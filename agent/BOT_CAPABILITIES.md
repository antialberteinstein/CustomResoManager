# Bot (AI Assistant) — Danh sách chức năng

Trợ lý chat tích hợp trong app. Người dùng nhắn bằng **tiếng Việt tự nhiên**; bot suy luận theo
vòng **ReAct** (Thought → Action → Observation) rồi gọi các **MCP tool** của app để thực hiện hành
động, hoặc tra cứu tài liệu để trả lời.

- Backend: FastAPI (`agent/main.py`), mô hình GGUF chạy cục bộ (`llama.cpp`).
- Kết nối tới MCP server của app (`mcpServer` trong `agent/config.yaml`) để điều khiển.
- Nguồn chức năng: các tool khai báo trong `agent/chat_service.py`.

---

## 1. Điều khiển độ phân giải

| Chức năng | Tool | Người dùng nói (ví dụ) |
|---|---|---|
| Xem các độ phân giải hệ thống hỗ trợ | `list_resolutions` | "có những độ phân giải nào?" |
| Xem độ phân giải hiện tại | `get_current_resolution` | "máy tôi đang ở độ phân giải nào?" |
| Đổi độ phân giải ngay lập tức | `change_resolution` | "đổi sang 1920x1080", "đổi qua số 2" |
| Khôi phục độ phân giải trước đó | `revert_resolution` | "khôi phục", "về cũ", "undo" |

- Chọn theo **số thứ tự**: sau khi liệt kê, nói "đổi qua 2" → bot tự tra số 2 trong danh sách.
- `revert_resolution` không cần nhập width/height — **server tự nhớ** độ phân giải cũ.
- Chỉ đổi được sang độ phân giải nằm trong danh sách hỗ trợ (nếu không, bot báo lỗi lịch sự).

---

## 2. Quản lý Profile (app → độ phân giải)

| Chức năng | Tool | Người dùng nói (ví dụ) |
|---|---|---|
| Liệt kê toàn bộ profile đã lưu | `list_profiles` | "liệt kê các profile", "có những app nào trong danh sách?" |
| Tạo/cập nhật profile cho một app | `add_profile` | "khi mở notepad thì để màn hình 1280x720" |
| Xoá profile của một app | `remove_profile` | "xoá profile của valorant" |
| Bật/tắt một profile (không xoá) | `set_profile_enabled` | "tắt profile csgo", "bật lại profile notepad" |

- `processName` là tên tiến trình **không có** đuôi `.exe` (vd `notepad`, `valorant`).
- `add_profile` tự tính tỷ lệ khung hình; có thể kèm refresh rate và trạng thái bật/tắt.
- Phân biệt quan trọng: **`change_resolution`** đổi màn hình *ngay*; **`add_profile`** chỉ *lưu cấu
  hình* để engine áp dụng khi app đó được focus ("khi mở app X thì…").

---

## 3. Điều khiển Engine (tự đổi theo app đang focus)

| Chức năng | Tool | Người dùng nói (ví dụ) |
|---|---|---|
| Bật engine theo dõi focus | `start_engine` | "bật engine lên", "bắt đầu tự đổi độ phân giải" |
| Tắt engine, trả về độ phân giải gốc | `stop_engine` | "tắt engine", "dừng lại" |
| Xem trạng thái engine + profile đang áp | `get_engine_status` | "engine đang chạy không?" |

- Khi engine chạy: app được focus khớp một profile **đang bật** → màn hình tự đổi; rời app → về gốc.
- Sau khi tạo/sửa profile, bot có thể nhắc bật engine để cấu hình có hiệu lực.

---

## 4. Khám phá tiến trình / ứng dụng trên máy

| Chức năng | Tool | Người dùng nói (ví dụ) |
|---|---|---|
| Liệt kê tiến trình đang chạy (có cửa sổ) | `list_running_processes` | "máy đang mở những app nào?" |
| Liệt kê ứng dụng đã cài | `list_installed_apps` | "trên máy có cài những app gì?" |

- Dùng để **tra đúng `processName`** khi người dùng gọi app bằng tên thường (vd "chrome", "trình duyệt").
- Quy trình thêm app vào profile: tra tên qua 2 tool này → gọi `add_profile`.
- **Hỏi lại khi thiếu độ phân giải:** nếu người dùng muốn thêm một app vào danh sách mà *chưa* cho biết
  width/height, bot **không tự bịa** — sẽ hỏi lại "bạn muốn độ phân giải nào?" rồi mới tạo profile.

---

## 5. Tra cứu tài liệu hướng dẫn

| Chức năng | Tool | Người dùng nói (ví dụ) |
|---|---|---|
| Trả lời câu hỏi về cách dùng phần mềm | `search_docs` | "phần mềm này dùng để làm gì?", "cách thêm app?" |

- Dùng RAG: tài liệu trong `agent/docs/` được index (Chroma + `all-MiniLM-L6-v2`), bot truy hồi đoạn
  liên quan rồi tóm tắt trả lời.

---

## Ghi chú

- Bot trả lời **bằng tiếng Việt**, và xuống dòng từng mục cho dễ đọc khi liệt kê.
- Mọi hành động đi qua MCP server của app — xem chi tiết tool/định dạng kết quả ở `../FEATURES.md` (mục 6).
- Nếu MCP server không kết nối được, bot báo lỗi và gợi ý thử lại, không làm treo hội thoại.
- Phạm vi hiện tại: độ phân giải + profile + engine + liệt kê tiến trình/ứng dụng để thêm vào profile.
- Khi bot thực hiện hành động (đổi độ phân giải, thêm/sửa/xoá profile, bật/tắt engine), **giao diện app
  tự cập nhật** nhờ các sự kiện đồng bộ (nút engine, danh sách profile, độ phân giải hiện tại).
