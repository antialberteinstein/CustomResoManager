from pathlib import Path
from typing import Optional

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
from vectorizer import Vectorizer, get_vectorizer

class ChatService:
    def __init__(self) -> None:
        self.gguf_url = GGUF_URL
        self.gguf_filename = GGUF_FILENAME
        self.temperature = TEMPERATURE
        self._model: Optional[Llama] = None
        self._embeddings: Optional[Vectorizer] = None
        self._collection = None

    def load(self) -> None:
        print("[chat] Preparing model file...")
        model_path = self._ensure_model_file()
        print(f"[chat] Loading model from: {model_path}")
        self._model = Llama(model_path=str(model_path), n_ctx=MODEL_CONTEXT_TOKENS)
        print("[chat] Model loaded.")
        self._embeddings = get_vectorizer()
        client = chromadb.PersistentClient(path=CHROMA_DIR)
        self._collection = client.get_or_create_collection(COLLECTION_NAME)

    def generate_reply(self, message: str) -> str:
        if self._model is None:
            raise RuntimeError("Model not loaded")

        if not message.strip():
            return ""

        if self._embeddings is None or self._collection is None:
            raise RuntimeError("Vector store not initialized")

        query_vector = self._embeddings.embed_query(message.strip())
        results = self._collection.query(
            query_embeddings=[query_vector],
            n_results=TOP_K,
            include=["documents", "metadatas"],
        )

        documents = results.get("documents", [])
        if not documents or not documents[0]:
            return "Khong tim thay thong tin phu hop trong tai lieu."

        context = "\n\n".join(documents[0])
        prompt = (
            "Bạn là trợ lý hướng dẫn sử dụng hệ thống. "
            "Chỉ trả lời dựa trên tài liệu được cung cấp. "
            "Hãy trả lời đúng trọng tâm câu hỏi, không tự diễn giải lan man. "
            "Nếu tài liệu không có thông tin trực tiếp, hãy nói rõ là không tìm thấy.\n\n"
            f"Tài liệu:\n{context}\n\n"
            f"Câu hỏi: {message.strip()}\n"
            "Trả lời ngắn gọn, đúng trọng tâm:"
        )

        prompt = self._trim_prompt(prompt)

        result = self._model.create_completion(
            prompt=prompt,
            max_tokens=MAX_TOKENS,
            temperature=self.temperature,
        )
        text = result["choices"][0]["text"]
        return self._dedupe_lines(self._clean_reply(text))

    def _clean_reply(self, text: str) -> str:
        cleaned = text.strip()
        if not cleaned:
            return cleaned

        if "Trả lời:" in cleaned:
            cleaned = cleaned.split("Trả lời:")[-1].strip()

        for marker in ("Câu hỏi:", "Tài liệu:"):
            if marker in cleaned:
                cleaned = cleaned.split(marker)[0].strip()

        return cleaned

    def _dedupe_lines(self, text: str) -> str:
        if not text:
            return text

        lines = [line.strip() for line in text.splitlines() if line.strip()]
        if not lines:
            return ""

        output_lines = []
        last = None
        repeats = 0
        for line in lines:
            if line == last:
                repeats += 1
                if repeats >= 2:
                    continue
            else:
                repeats = 0
                last = line

            output_lines.append(line)

        return "\n".join(output_lines)

    def _trim_prompt(self, prompt: str) -> str:
        if self._model is None:
            return prompt

        max_prompt_tokens = max(MODEL_CONTEXT_TOKENS - 128, 64)
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
