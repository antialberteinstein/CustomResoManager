namespace CustomResoManager.Models
{
    public class GameProfile
    {
        /// <summary>
        /// Tên tiến trình của game (VD: "csgo", "valorant"). Không bao gồm ".exe".
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Chiều rộng độ phân giải mục tiêu khi bật game (VD: 800).
        /// </summary>
        public int TargetWidth { get; set; }

        /// <summary>
        /// Chiều cao độ phân giải mục tiêu khi bật game (VD: 600).
        /// </summary>
        public int TargetHeight { get; set; }

        /// <summary>
        /// (Tùy chọn) Tần số quét mục tiêu. Nếu null sẽ dùng mặc định của hệ thống.
        /// </summary>
        public int? TargetRefreshRate { get; set; }

        /// <summary>
        /// Tỷ lệ khung hình mong muốn (VD: "4:3"). Chủ yếu dùng để UI hiển thị hoặc tính toán log.
        /// </summary>
        public string ExpectedAspectRatio { get; set; } = string.Empty;

        /// <summary>
        /// Trạng thái bật/tắt (coi như người dùng tạm thời không muốn dùng profile này).
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}