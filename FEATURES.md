# Custom Resolution Manager — Danh sách chức năng

Ứng dụng WPF (.NET 8, `net8.0-windows`) trên Windows giúp **tự động đổi độ phân giải màn hình theo ứng dụng đang được focus**, kèm một **MCP Server** để AI agent có thể điều khiển độ phân giải, và một **AI Assistant** (trợ lý chat) tích hợp ngay trong app.

Toàn bộ thao tác màn hình dùng **Win32 API** (`ChangeDisplaySettings`, `EnumDisplaySettings`, `SetWinEventHook`).

---

## 1. Engine đổi độ phân giải theo focus

Chức năng lõi (bật/tắt bằng nút **▶ Start Engine / ⏹ Stop Engine** ở thanh tiêu đề).

| Chức năng | Mô tả |
|---|---|
| Theo dõi cửa sổ foreground | Dùng `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` — phản hồi tức thì khi đổi app (không polling). |
| Đổi độ phân giải theo app | App đang focus khớp một **profile đang bật** → màn hình tự đổi sang độ phân giải của profile đó. |
| Khôi phục khi rời app | Focus sang app **ngoài danh sách** (kể cả chính app này) → trả về **độ phân giải gốc** (baseline lúc Start). |
| Mỗi app một độ phân giải riêng | A = 800×600, B = 1280×768 → focus A đổi qua 800×600, focus B đổi qua 1280×768. |
| Hỗ trợ refresh rate | Profile có thể đặt tần số quét (Hz) riêng; bỏ trống = dùng mặc định hệ thống. |
| Stop → trả về gốc | Bấm Stop bất kỳ lúc nào đều trả màn hình về độ phân giải gốc. |

### Tối ưu chống giật/nhấp nháy
- **Dedup**: chỉ đổi khi độ phân giải đích khác hiện tại (A→B cùng độ phân giải = không đổi).
- **Latest-wins**: alt-tab nhanh A→B→A chỉ áp lần focus cuối (`_generation`).
- **Debounce 300ms**: bỏ qua focus thoáng qua (preview alt-tab, taskbar).
- **Cooldown 500ms**: giữ khoảng cách tối thiểu giữa 2 lần đổi mode thật (hoãn, không bỏ).
- **Cờ `CDS_FULLSCREEN`**: đổi mode tạm thời, không ghi registry, ít overhead, tự revert khi tiến trình thoát.

> Giới hạn: đổi độ phân giải **thật** luôn có ~0.3–1.5s màn hình re-sync ở cấp phần cứng — chỉ giảm tần suất, không khử hẳn.

---

## 2. Quản lý Profile

Mỗi profile = 1 ứng dụng + cấu hình độ phân giải. Lưu tại `profiles.json` (cùng thư mục app).

**Cấu trúc một profile (`GameProfile`):**

| Trường | Kiểu | Ý nghĩa |
|---|---|---|
| `ProcessName` | string | Tên tiến trình, không đuôi `.exe` (vd `"notepad"`) |
| `TargetWidth` / `TargetHeight` | int | Độ phân giải mục tiêu (vd 800 × 600) |
| `TargetRefreshRate` | int? | Tần số quét mục tiêu (null = mặc định) |
| `ExpectedAspectRatio` | string | Tỷ lệ khung hình tính sẵn (vd `"4:3"`) — hiển thị |
| `IsEnabled` | bool | Bật/tắt profile mà không cần xoá |

**Thao tác trên danh sách (DataGrid):**
- Thêm profile (form nhập tên + chọn độ phân giải + refresh rate → **Save**).
- **Sửa độ phân giải ngay trong danh sách** (ComboBox inline mỗi dòng).
- Bật/tắt từng profile (checkbox cột **On**).
- Xoá profile (nút **Delete**, có xác nhận).
- Hiển thị: Process, Resolution, Ratio, Hz.
- Banner "Profile active" khi một profile đang được áp dụng.

---

## 3. Ba nguồn thêm app vào danh sách

Cùng nằm trên một hàng trong khu **Add Profile**:

| Nút | Chức năng |
|---|---|
| 📁 **Chọn file** | Mở hộp thoại chọn file `.exe` hoặc shortcut `.lnk`; `.lnk` được resolve về `.exe` đích (qua `WScript.Shell`) để lấy tên tiến trình. |
| 🗔 **Tất cả app** | Liệt kê **mọi app đã cài** (quét shortcut `*.lnk` trong Start Menu máy + người dùng), có ô tìm kiếm; chọn → resolve về tên tiến trình. |
| ⚙ **Tiến trình chạy** | Liệt kê **các tiến trình đang chạy** có cửa sổ thật (tên + tiêu đề), có ô tìm kiếm; chọn → điền tên tiến trình. |

Ngoài ra vẫn có thể **gõ tay** tên tiến trình.

---

## 4. AI Assistant (trợ lý chat tích hợp)

Panel riêng bên phải, chiếm toàn bộ chiều cao app.

| Chức năng | Mô tả |
|---|---|
| Hộp chat | Gửi câu hỏi/tiếng Việt tới agent, hiển thị hội thoại. |
| Trạng thái kết nối | Chấm + nhãn Connected/Disconnected tới agent. |
| Endpoint | Gửi `POST http://127.0.0.1:8000/chat` `{ "message": "..." }` → nhận `{ "reply": "..." }`. |
| Health check | Kiểm tra `GET http://127.0.0.1:8000/health` lúc khởi động. |

---

## 5. Vòng đời ứng dụng

- **Khởi động**: tạo `ResolutionManager`, `ProfileManager`, `AppEngine`; khởi chạy **MCP Server in-process** (port 7777).
- **Đóng app (nút X)**: trả về độ phân giải gốc rồi **kết liễu tiến trình ngay** (`Environment.Exit(0)`) — MCP server in-process cũng tắt theo, không còn gì chạy nền.

---

## 6. MCP Server — điều khiển app bằng AI agent

Đây là phần cho phép **agent điều khiển app**. Server hiện các *tool* qua giao thức **MCP (streamable-HTTP)**.

- **Endpoint**: `http://127.0.0.1:7777/mcp/`
- **Chạy ở 2 nơi**: in-process trong app WPF (`App.xaml.cs`), và project độc lập `CustomResoManager.McpServer` (`Program.cs`).
- **Backed by Win32** (`Core/ResolutionManager.cs`) — đổi độ phân giải thật.
- **Trạng thái "previous"** giữ ở server (`ServerState`) → `revert_resolution` vẫn đúng kể cả khi agent khởi động lại.

### Các MCP tools

**Nhóm độ phân giải** (backed by `Core/ResolutionManager.cs`):

| Tool | Tham số | Kết quả | Ý nghĩa |
|---|---|---|---|
| `list_resolutions` | — | `[{width,height,refreshRate,isNative}]` | Liệt kê các độ phân giải màn hình chính hỗ trợ. |
| `get_current_resolution` | — | `{width,height,refreshRate}` | Lấy độ phân giải hiện tại. |
| `change_resolution` | `width:int, height:int` | `{success, applied, previous, error}` | Đổi độ phân giải (validate theo danh sách hỗ trợ). |
| `revert_resolution` | — | `{success, applied, previous, error}` | Quay lại độ phân giải trước lần đổi gần nhất. |

**Nhóm quản lý profile** (backed by `Core/ProfileManager.cs` — ghi vào `profiles.json`):

| Tool | Tham số | Kết quả | Ý nghĩa |
|---|---|---|---|
| `list_profiles` | — | `[{processName,width,height,refreshRate,aspectRatio,enabled}]` | Liệt kê toàn bộ profile đã lưu. |
| `add_profile` | `processName:str, width:int, height:int, refreshRate:int?, enabled:bool?` | `{success, profile, error}` | Tạo mới / cập nhật profile cho một app (tự tính tỷ lệ khung hình). |
| `remove_profile` | `processName:str` | `{success, profile, error}` | Xoá profile của một app. |
| `set_profile_enabled` | `processName:str, enabled:bool` | `{success, profile, error}` | Bật/tắt profile mà không xoá. |

**Nhóm điều khiển engine** (backed by `Core/AppEngine.cs`):

| Tool | Tham số | Kết quả | Ý nghĩa |
|---|---|---|---|
| `start_engine` | — | `{running, activeProfile, error}` | Bật engine tự đổi độ phân giải theo app đang focus. |
| `stop_engine` | — | `{running, activeProfile, error}` | Tắt engine và trả màn hình về độ phân giải gốc. |
| `get_engine_status` | — | `{running, activeProfile, error}` | Xem engine đang chạy hay không và profile đang áp. |

**Nhóm khám phá tiến trình / app** (để agent tra `processName` rồi tạo profile):

| Tool | Tham số | Kết quả | Ý nghĩa |
|---|---|---|---|
| `list_running_processes` | — | `[{processName,title}]` | Liệt kê tiến trình đang chạy có cửa sổ thật. |
| `list_installed_apps` | — | `[{name,processName}]` | Liệt kê app đã cài (quét shortcut Start Menu, resolve về `.exe`). |

> Khi agent gọi tool làm đổi trạng thái (đổi độ phân giải, thêm/sửa/xoá profile, bật/tắt engine),
> giao diện WPF **tự cập nhật** nhờ các event `ResolutionChanged` / `ProfilesChanged` / `Started` / `Stopped`.

> Engine start/stop chỉ thật sự nhận sự kiện focus khi chạy **in-process trong app WPF** (cần message pump trên UI thread) — server đẩy lời gọi lên UI dispatcher. Bản `CustomResoManager.McpServer` độc lập (console) vẫn expose các tool này nhưng engine không nhận được WinEvent (dùng cho dev/test).

**Ví dụ kết quả `change_resolution`:**
```json
{
  "success": true,
  "applied":  { "width": 1280, "height": 720 },
  "previous": { "width": 1920, "height": 1080, "refreshRate": 60 },
  "error": null
}
```
Khi thất bại: `success:false`, `applied:null`, `error` mô tả lý do (vd độ phân giải không được hỗ trợ).

---

## 7. AI Agent đi kèm (Python)

Thư mục `agent/` — backend trả lời chat và gọi MCP tools.

| Thành phần | Mô tả |
|---|---|
| Server chat | FastAPI tại `127.0.0.1:8000` (`/chat`, `/health`). |
| LLM | Mô hình GGUF chạy cục bộ (`llama.cpp`), điều khiển theo vòng lặp **ReAct**. |
| RAG | Tài liệu hướng dẫn (`docs/`) được index bằng Chroma + `all-MiniLM-L6-v2` để trả lời theo ngữ cảnh. |
| MCP client | Kết nối `http://127.0.0.1:7777/mcp/`, gọi các tool ở mục 6 (độ phân giải + profile + engine). |

---

## 8. Cách dựng một agent điều khiển app

App **mở sẵn cổng điều khiển qua MCP**, nên bất kỳ MCP client nào cũng dùng được:

1. Đảm bảo app WPF (hoặc `CustomResoManager.McpServer`) đang chạy → MCP sống ở `http://127.0.0.1:7777/mcp/`.
2. Dùng MCP client (transport **streamable-HTTP**) kết nối, `initialize`, rồi `list_tools`.
3. Gọi tool để điều khiển app:
   - Độ phân giải: `get_current_resolution` / `list_resolutions` để đọc, `change_resolution(width, height)` để đổi, `revert_resolution()` để hoàn tác.
   - Profile: `list_profiles`, `add_profile(...)`, `remove_profile(...)`, `set_profile_enabled(...)`.
   - Engine: `start_engine`, `stop_engine`, `get_engine_status`.
4. (Tùy chọn) Bọc bằng một LLM theo vòng **ReAct** để dịch yêu cầu ngôn ngữ tự nhiên → lời gọi tool (xem `agent/` làm mẫu — `chat_service.py` đã hỗ trợ toàn bộ tool ở trên).

> Phạm vi điều khiển của agent hiện tại: **đọc/đổi độ phân giải, quản lý danh sách profile, và bật/tắt engine** — gần như toàn bộ thao tác lõi của app. (Các hộp thoại chọn app/tiến trình vẫn là thao tác GUI thuần.)

---

## Phụ lục — cổng & file

| Hạng mục | Giá trị |
|---|---|
| MCP server | `http://127.0.0.1:7777/mcp/` |
| Agent chat API | `http://127.0.0.1:8000` (`/chat`, `/health`) |
| File profile | `profiles.json` (cạnh file thực thi) |
| Nguồn "app đã cài" | Shortcut `*.lnk` trong Start Menu (máy + người dùng) |
