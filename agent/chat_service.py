import asyncio
import json
import os
import re
from pathlib import Path
from typing import Any, Optional


_DEBUG = bool(os.environ.get("CHAT_DEBUG"))

# Tools forwarded straight to the MCP server (profile CRUD + engine control). Resolution
# tools stay special-cased because they also update local state (_last_listed_resolutions,
# _previous_resolution) used by the ReAct prompt.
_PASSTHROUGH_TOOLS = frozenset({
    "list_profiles",
    "add_profile",
    "remove_profile",
    "set_profile_enabled",
    "start_engine",
    "stop_engine",
    "get_engine_status",
})

import chromadb
import requests
from llama_cpp import Llama

from config import (
    CHROMA_DIR,
    COLLECTION_NAME,
    GGUF_FILENAME,
    GGUF_URL,
    MODEL_CONTEXT_TOKENS,
    TEMPERATURE,
    TOP_K,
    MAX_TOKENS,
)
from mcp_client import MCPToolingClient, MCPUnavailableError
from vectorizer import Vectorizer, get_vectorizer


REACT_SYSTEM_PROMPT = """Bạn là trợ lý quản lý độ phân giải màn hình. Bạn giải quyết yêu cầu của người dùng bằng cách suy luận theo pattern ReAct: Thought → Action → Observation, lặp đến khi có đủ thông tin để đưa Final Answer.

TOOL có sẵn:
- list_resolutions: lấy danh sách độ phân giải hệ thống hỗ trợ. Input: {}.
- get_current_resolution: lấy độ phân giải đang dùng của màn hình. Input: {}.
- change_resolution: đổi độ phân giải màn hình. Input: {"width": <int>, "height": <int>}.
- revert_resolution: khôi phục về độ phân giải ngay trước lần đổi gần nhất. Server tự nhớ giá trị cũ nên không cần truyền width/height. Input: {}.
- list_profiles: liệt kê các profile đã lưu (mỗi profile = 1 app + độ phân giải mục tiêu). Input: {}.
- add_profile: tạo mới hoặc cập nhật profile cho một app. processName là tên tiến trình KHÔNG có ".exe" (vd "notepad"). refreshRate và enabled là tùy chọn. Input: {"processName": "<str>", "width": <int>, "height": <int>, "refreshRate": <int|null>, "enabled": <bool>}.
- remove_profile: xoá profile của một app. Input: {"processName": "<str>"}.
- set_profile_enabled: bật/tắt một profile mà không xoá. Input: {"processName": "<str>", "enabled": <bool>}.
- start_engine: bật engine tự đổi độ phân giải theo app đang focus. Input: {}.
- stop_engine: tắt engine và trả màn hình về độ phân giải gốc. Input: {}.
- get_engine_status: xem engine đang chạy hay không và profile nào đang được áp. Input: {}.
- search_docs: tra cứu tài liệu hướng dẫn sử dụng phần mềm. Input: {"query": "<câu truy vấn>"}.

QUY TẮC FORMAT (BẮT BUỘC TUÂN THỦ):
- Mỗi bước, output đúng 3 dòng:
  Thought: <suy luận của bạn>
  Action: <tên tool>
  Action Input: <JSON object hợp lệ trên một dòng>
- Sau Action Input, DỪNG LẠI hoàn toàn. KHÔNG được tự bịa "Observation:" — hệ thống sẽ thêm vào.
- Khi đã đủ thông tin để trả lời, KẾT THÚC bằng:
  Thought: <suy luận cuối>
  Final Answer: <câu trả lời cuối cho người dùng bằng tiếng Việt tự nhiên>
- Sau Final Answer, DỪNG LẠI ngay lập tức. TUYỆT ĐỐI KHÔNG lặp lại Thought, Final Answer, hay bất kỳ nội dung nào nữa.
- KHÔNG bao giờ output text trống/ rác / nội dung không thuộc format trên trước Thought.

VÍ DỤ 1 — xem danh sách (Final Answer PHẢI xuống dòng từng độ phân giải, KHÔNG inline bằng dấu phẩy):
User: có những độ phân giải nào?
Thought: Người dùng muốn xem danh sách, mình gọi list_resolutions.
Action: list_resolutions
Action Input: {}
Observation: 1. 3840x2160 @ 60Hz (native)
2. 1920x1080 @ 60Hz
Thought: Đã có danh sách, trình bày từng dòng cho dễ đọc.
Final Answer: Hệ thống đang hỗ trợ các độ phân giải sau:
1. 3840x2160 @ 60Hz (native)
2. 1920x1080 @ 60Hz
Bạn có thể nói "đổi sang số <n>" để chọn.

VÍ DỤ 2 — đổi bằng số thứ tự (dùng state đã hiển thị):
[State: Danh sách gần nhất: 1. 3840x2160 @ 60Hz; 2. 1920x1080 @ 60Hz]
User: đổi qua 2
Thought: Số 2 trong danh sách là 1920x1080, mình gọi change_resolution.
Action: change_resolution
Action Input: {"width": 1920, "height": 1080}
Observation: {"success": true, "applied": {"width": 1920, "height": 1080}, "previous": {"width": 3840, "height": 2160, "refreshRate": 60}}
Thought: Đổi thành công, báo cho người dùng và gợi ý revert.
Final Answer: Đã đổi sang 1920x1080. Nếu muốn quay lại độ phân giải cũ, bạn chỉ cần nói "khôi phục" nhé.

VÍ DỤ 3 — revert (server tự nhớ độ phân giải cũ, KHÔNG cần truyền width/height):
User: khôi phục
Thought: Người dùng muốn quay lại độ phân giải trước đó, mình gọi revert_resolution.
Action: revert_resolution
Action Input: {}
Observation: {"success": true, "applied": {"width": 3840, "height": 2160}, "previous": {"width": 1920, "height": 1080, "refreshRate": 60}}
Thought: Đã khôi phục, báo cho người dùng.
Final Answer: Đã khôi phục độ phân giải về 3840x2160.

VÍ DỤ 4 — xem độ phân giải hiện tại:
User: máy tôi đang ở độ phân giải nào?
Thought: Người dùng muốn biết độ phân giải hiện tại, mình gọi get_current_resolution.
Action: get_current_resolution
Action Input: {}
Observation: {"width": 1920, "height": 1080, "refreshRate": 60}
Thought: Đã có thông tin, trả lời người dùng.
Final Answer: Máy bạn đang ở độ phân giải 1920x1080 @ 60Hz.

VÍ DỤ 5 — câu hỏi về tài liệu:
User: phần mềm này dùng để làm gì?
Thought: Đây là câu hỏi về tài liệu hướng dẫn, mình search.
Action: search_docs
Action Input: {"query": "phần mềm này dùng để làm gì"}
Observation: <nội dung tài liệu>
Thought: Đã có thông tin, trả lời người dùng.
Final Answer: <tóm tắt từ tài liệu>

VÍ DỤ 6 — tạo profile cho một app:
User: khi mở notepad thì để màn hình 1280x720
Thought: Người dùng muốn tạo profile cho notepad ở 1280x720, mình gọi add_profile.
Action: add_profile
Action Input: {"processName": "notepad", "width": 1280, "height": 720}
Observation: {"success": true, "profile": {"processName": "notepad", "width": 1280, "height": 720, "refreshRate": null, "aspectRatio": "16:9", "enabled": true}}
Thought: Đã tạo profile, báo cho người dùng.
Final Answer: Đã tạo profile: khi notepad được focus màn hình sẽ chuyển sang 1280x720. Nhớ bật engine để nó tự áp dụng nhé.

VÍ DỤ 7 — bật engine:
User: bật engine lên
Thought: Người dùng muốn bật engine theo dõi focus, mình gọi start_engine.
Action: start_engine
Action Input: {}
Observation: {"running": true, "activeProfile": null}
Thought: Engine đã chạy, báo cho người dùng.
Final Answer: Đã bật engine. Từ giờ màn hình sẽ tự đổi độ phân giải theo app đang focus.

LƯU Ý:
- Chỉ dùng width/height có trong danh sách hỗ trợ.
- Nếu user nói "số N" hoặc bare number N, tra cứu state "Danh sách gần nhất" để xác định width/height.
- Nếu user yêu cầu revert/khôi phục/undo/về cũ, gọi revert_resolution với Input {} (server tự biết độ phân giải cũ, không cần width/height).
- Nếu state "Danh sách gần nhất" rỗng mà user chọn theo số, gọi list_resolutions trước.
- processName luôn là tên tiến trình KHÔNG kèm ".exe" (vd "valorant", "notepad"). Nếu user đưa tên có ".exe" hay đường dẫn, lấy phần tên file không đuôi.
- Phân biệt: change_resolution đổi NGAY độ phân giải hiện tại; add_profile chỉ lưu cấu hình để engine áp khi app đó được focus. "Đổi luôn" -> change_resolution; "khi mở app X thì..." -> add_profile.
- Sau khi tạo/sửa profile, nếu engine chưa chạy có thể nhắc user bật engine (start_engine) để áp dụng.
- Nếu observation báo lỗi (ERROR / success=false), đưa Final Answer giải thích lịch sự cho người dùng."""


class ChatService:
    def __init__(self) -> None:
        self.gguf_url = GGUF_URL
        self.gguf_filename = GGUF_FILENAME
        self.temperature = TEMPERATURE
        self._model: Optional[Llama] = None
        self._embeddings: Optional[Vectorizer] = None
        self._collection = None
        self._tooling: Optional[MCPToolingClient] = None
        self._last_listed_resolutions: list[dict] = []
        self._previous_resolution: Optional[dict] = None

    async def load(self) -> None:
        print("[chat] Preparing model file...")
        model_path = await asyncio.to_thread(self._ensure_model_file)
        print(f"[chat] Loading model from: {model_path}")
        self._model = await asyncio.to_thread(
            lambda: Llama(model_path=str(model_path), n_ctx=MODEL_CONTEXT_TOKENS)
        )
        print("[chat] Model loaded.")
        self._embeddings = await asyncio.to_thread(get_vectorizer)
        client = await asyncio.to_thread(chromadb.PersistentClient, path=CHROMA_DIR)
        self._collection = client.get_or_create_collection(COLLECTION_NAME)

        self._tooling = MCPToolingClient()
        await self._tooling.start()

    async def close(self) -> None:
        if self._tooling is not None:
            await self._tooling.close()
            self._tooling = None

    async def chat(self, message: str) -> str:
        if not message.strip():
            return ""
        return await self._react_loop(message)

    async def _react_loop(self, message: str, max_steps: int = 5) -> str:
        scratchpad = ""
        for step in range(max_steps):
            prompt = self._build_react_prompt(message, scratchpad)
            text = await self._llm_raw(
                prompt,
                max_tokens=400,
                stop=["Observation:", "\nUser:", "\n\nUser:"],
            )
            if _DEBUG:
                print(f"[react] step={step} raw={text.strip()!r}")

            parsed = self._parse_react_step(text)

            if "final_answer" in parsed:
                final = parsed["final_answer"]
                return self._dedupe_lines(self._clean_reply(final))

            if "action" in parsed:
                action = parsed["action"]
                args = parsed.get("action_input", {})
                observation = await self._execute_action(action, args)
                if _DEBUG:
                    print(f"[react] action={action} args={args}")
                scratchpad += (
                    f"{text.rstrip()}\nObservation: {observation}\n"
                )
                continue

            return self._dedupe_lines(self._clean_reply(text))

        return (
            "Mình chưa hoàn tất yêu cầu của bạn trong giới hạn các bước cho phép. "
            "Bạn thử nói lại rõ hơn nhé."
        )

    def _build_react_prompt(self, message: str, scratchpad: str) -> str:
        if self._last_listed_resolutions:
            last_listed = "\n".join(
                f"  {i}. {r['width']}x{r['height']} @ {r['refreshRate']}Hz"
                + (" (native)" if r.get("isNative") else "")
                for i, r in enumerate(self._last_listed_resolutions, start=1)
            )
        else:
            last_listed = "  (chưa hiển thị)"

        prev = self._previous_resolution
        prev_str = (
            f"{prev['width']}x{prev['height']}"
            if prev
            else "(chưa có)"
        )

        state_block = (
            "STATE HIỆN TẠI:\n"
            f"- Danh sách gần nhất đã hiển thị:\n{last_listed}\n"
            f"- Độ phân giải cũ (để revert): {prev_str}\n"
        )

        return (
            f"{REACT_SYSTEM_PROMPT}\n\n"
            f"{state_block}\n"
            f"User: {message.strip()}\n"
            f"{scratchpad}"
        )

    def _parse_react_step(self, text: str) -> dict:
        final_match = re.search(
            r"Final Answer:\s*(.+?)(?:\nThought:|\nAction:|\nObservation:|\nUser:|\Z)",
            text,
            re.DOTALL,
        )
        if final_match:
            return {"final_answer": final_match.group(1).strip()}

        action_match = re.search(r"Action:\s*([A-Za-z_][A-Za-z0-9_]*)", text)
        if not action_match:
            return {}

        action = action_match.group(1).strip()

        input_match = re.search(
            r"Action Input:\s*(\{.*?\})",
            text,
            re.DOTALL,
        )
        args: dict[str, Any] = {}
        if input_match:
            try:
                args = json.loads(input_match.group(1))
            except json.JSONDecodeError:
                args = {}

        return {"action": action, "action_input": args}

    async def _execute_action(self, action: str, args: dict) -> str:
        if action == "list_resolutions":
            if self._tooling is None:
                return "ERROR: tooling không khả dụng. Hãy thông báo cho người dùng."
            try:
                resolutions = await self._tooling.call_tool(
                    "list_resolutions", {}
                )
            except MCPUnavailableError:
                return "ERROR: MCP server không kết nối được. Hãy thông báo cho người dùng và gợi ý thử lại sau."

            if not resolutions:
                return "Danh sách rỗng."

            self._last_listed_resolutions = list(resolutions)
            return "\n".join(
                f"{i}. {r['width']}x{r['height']} @ {r['refreshRate']}Hz"
                + (" (native)" if r.get("isNative") else "")
                for i, r in enumerate(resolutions, start=1)
            )

        if action == "get_current_resolution":
            if self._tooling is None:
                return "ERROR: tooling không khả dụng. Hãy thông báo cho người dùng."
            try:
                result = await self._tooling.call_tool(
                    "get_current_resolution", {}
                )
            except MCPUnavailableError:
                return "ERROR: MCP server không kết nối được. Hãy thông báo cho người dùng và gợi ý thử lại sau."
            return json.dumps(result or {}, ensure_ascii=False)

        if action == "change_resolution":
            if self._tooling is None:
                return "ERROR: tooling không khả dụng."
            try:
                width = int(args.get("width"))
                height = int(args.get("height"))
            except (TypeError, ValueError):
                return "ERROR: width và height phải là số nguyên."

            try:
                result = await self._tooling.call_tool(
                    "change_resolution",
                    {"width": width, "height": height},
                )
            except MCPUnavailableError:
                return "ERROR: MCP server không kết nối được."

            if result and result.get("success"):
                prev = result.get("previous")
                if prev:
                    self._previous_resolution = prev
            return json.dumps(result or {}, ensure_ascii=False)

        if action == "revert_resolution":
            if self._tooling is None:
                return "ERROR: tooling không khả dụng."
            try:
                result = await self._tooling.call_tool("revert_resolution", {})
            except MCPUnavailableError:
                return "ERROR: MCP server không kết nối được."

            if result and result.get("success"):
                prev = result.get("previous")
                if prev:
                    self._previous_resolution = prev
            return json.dumps(result or {}, ensure_ascii=False)

        # Profile & engine tools: thin pass-through to the MCP server. The server validates
        # inputs and returns a JSON result the LLM reads in the next Observation.
        if action in _PASSTHROUGH_TOOLS:
            if self._tooling is None:
                return "ERROR: tooling không khả dụng."
            tool_args = self._passthrough_args(action, args)
            if isinstance(tool_args, str):  # validation error message
                return tool_args
            try:
                result = await self._tooling.call_tool(action, tool_args)
            except MCPUnavailableError:
                return "ERROR: MCP server không kết nối được. Hãy thông báo cho người dùng và gợi ý thử lại sau."
            return json.dumps(result or {}, ensure_ascii=False)

        if action == "search_docs":
            query = args.get("query") or ""
            if not query.strip():
                return "ERROR: thiếu query."
            return await self._search_docs(query)

        return f"ERROR: tool '{action}' không tồn tại."

    def _passthrough_args(self, action: str, args: dict) -> Any:
        """Coerce/validate args for profile & engine tools. Returns a dict to send, or an
        ERROR string the LLM should surface to the user."""
        if action in ("list_profiles", "start_engine", "stop_engine", "get_engine_status"):
            return {}

        if action == "add_profile":
            name = str(args.get("processName") or "").strip()
            if not name:
                return "ERROR: thiếu processName (tên tiến trình)."
            try:
                payload: dict[str, Any] = {
                    "processName": name,
                    "width": int(args["width"]),
                    "height": int(args["height"]),
                }
            except (KeyError, TypeError, ValueError):
                return "ERROR: add_profile cần processName, width, height hợp lệ."
            if args.get("refreshRate") not in (None, ""):
                try:
                    payload["refreshRate"] = int(args["refreshRate"])
                except (TypeError, ValueError):
                    return "ERROR: refreshRate phải là số nguyên."
            if "enabled" in args and args["enabled"] is not None:
                payload["enabled"] = bool(args["enabled"])
            return payload

        if action == "remove_profile":
            name = str(args.get("processName") or "").strip()
            if not name:
                return "ERROR: thiếu processName (tên tiến trình)."
            return {"processName": name}

        if action == "set_profile_enabled":
            name = str(args.get("processName") or "").strip()
            if not name:
                return "ERROR: thiếu processName (tên tiến trình)."
            if args.get("enabled") is None:
                return "ERROR: set_profile_enabled cần enabled (true/false)."
            return {"processName": name, "enabled": bool(args["enabled"])}

        return {}

    async def _search_docs(self, query: str) -> str:
        if self._embeddings is None or self._collection is None:
            return "ERROR: vector store chưa init."

        query_vector = await asyncio.to_thread(
            self._embeddings.embed_query, query.strip()
        )
        results = await asyncio.to_thread(
            self._collection.query,
            query_embeddings=[query_vector],
            n_results=TOP_K,
            include=["documents", "metadatas"],
        )

        documents = results.get("documents", [])
        if not documents or not documents[0]:
            return "Không tìm thấy thông tin liên quan trong tài liệu."

        return "\n\n".join(documents[0])

    async def _llm_raw(
        self,
        prompt: str,
        max_tokens: int = 400,
        stop: Optional[list[str]] = None,
    ) -> str:
        if self._model is None:
            raise RuntimeError("Model not loaded")

        prompt = self._trim_prompt(prompt)
        result = await asyncio.to_thread(
            self._model.create_completion,
            prompt=prompt,
            max_tokens=max_tokens,
            temperature=self.temperature,
            stop=stop or [],
        )
        return result["choices"][0]["text"]

    def _clean_reply(self, text: str) -> str:
        cleaned = text.strip()
        if not cleaned:
            return cleaned

        if "Final Answer:" in cleaned:
            cleaned = cleaned.split("Final Answer:", 1)[1].strip()

        for marker in ("Thought:", "Action:", "Observation:", "User:"):
            idx = cleaned.find(marker)
            if idx != -1:
                cleaned = cleaned[:idx].strip()

        return cleaned

    def _dedupe_lines(self, text: str) -> str:
        if not text:
            return text

        lines = [line.rstrip() for line in text.splitlines()]
        if not any(line.strip() for line in lines):
            return ""

        output_lines: list[str] = []
        last = None
        repeats = 0
        for line in lines:
            if line.strip() and line == last:
                repeats += 1
                if repeats >= 2:
                    continue
            else:
                repeats = 0
                last = line
            output_lines.append(line)

        while output_lines and not output_lines[0].strip():
            output_lines.pop(0)
        while output_lines and not output_lines[-1].strip():
            output_lines.pop()

        return "\n".join(output_lines)

    def _trim_prompt(self, prompt: str) -> str:
        if self._model is None:
            return prompt

        max_prompt_tokens = max(MODEL_CONTEXT_TOKENS - 512, 64)
        tokens = self._model.tokenize(prompt.encode("utf-8"))
        if len(tokens) <= max_prompt_tokens:
            return prompt

        trimmed_tokens = tokens[-max_prompt_tokens:]
        trimmed = self._model.detokenize(trimmed_tokens).decode("utf-8", errors="ignore")
        return trimmed

    def _ensure_model_file(self) -> Path:
        cache_dir = self._get_hf_cache_dir()
        if cache_dir is not None:
            cache_path = cache_dir / self.gguf_filename
            if cache_path.exists():
                print(f"[chat] Using cached model: {cache_path}")
                return cache_path
            try:
                print(f"[chat] Downloading model to cache: {cache_path}")
                return self._download(self.gguf_url, cache_path)
            except OSError:
                pass

        fallback_dir = Path(".models")
        fallback_dir.mkdir(parents=True, exist_ok=True)
        fallback_path = fallback_dir / self.gguf_filename
        if fallback_path.exists():
            print(f"[chat] Using local model: {fallback_path}")
            return fallback_path

        print(f"[chat] Downloading model to local path: {fallback_path}")
        return self._download(self.gguf_url, fallback_path)

    def _get_hf_cache_dir(self) -> Optional[Path]:
        base = Path.home() / ".cache" / "huggingface"
        try:
            base.mkdir(parents=True, exist_ok=True)
            test_path = base / ".write_test"
            test_path.write_text("ok")
            test_path.unlink(missing_ok=True)
            return base
        except OSError:
            return None

    def _download(self, url: str, dest: Path) -> Path:
        dest.parent.mkdir(parents=True, exist_ok=True)
        tmp = dest.with_suffix(dest.suffix + ".part")
        with requests.get(url, stream=True, timeout=60) as response:
            response.raise_for_status()
            total = int(response.headers.get("Content-Length", "0"))
            downloaded = 0
            with open(tmp, "wb") as file:
                for chunk in response.iter_content(chunk_size=1024 * 1024):
                    if chunk:
                        file.write(chunk)
                        downloaded += len(chunk)
                        if total > 0:
                            percent = int(downloaded * 100 / total)
                            print(f"[chat] Downloading... {percent}%", end="\r")
        if total > 0:
            print("[chat] Downloading... 100%")
        tmp.replace(dest)
        return dest
