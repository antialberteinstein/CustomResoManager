using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CustomResoManager.Models;

namespace CustomResoManager.Core
{
    public interface IProfileManager
    {
        List<GameProfile> GetAllProfiles();
        GameProfile? GetProfile(string processName);
        void AddOrUpdateProfile(GameProfile profile);
        bool RemoveProfile(string processName);
        void SaveChanges();
        void LoadProfiles();

        /// <summary>Raised after the profile list is persisted (add/update/remove), so the UI can refresh
        /// even when the change came from the MCP/agent rather than the user.</summary>
        event EventHandler? ProfilesChanged;
    }

    public class ProfileManager : IProfileManager
    {
        private readonly string _profilesFilePath;
        private List<GameProfile> _profiles;

        public event EventHandler? ProfilesChanged;

        public ProfileManager(string filePath = "profiles.json")
        {
            // Mặc định ném file ra chung thư mục app path / bin
            _profilesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
            _profiles = new List<GameProfile>();
            
            LoadProfiles(); // Tự động load khi khởi tạo
        }

        /// <summary>
        /// Lấy toàn bộ danh sách thiết lập giải độ phân giải.
        /// </summary>
        public List<GameProfile> GetAllProfiles()
        {
            return _profiles.ToList(); // Clone để tránh sửa trực tiếp List<T> bên dưới
        }

        /// <summary>
        /// Gets 1 profile theo Tên Process
        /// </summary>
        public GameProfile? GetProfile(string processName)
        {
            return _profiles.FirstOrDefault(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Thêm mới nếu chưa có, Cập nhật nếu đã có. Tự động SaveChanges().
        /// </summary>
        public void AddOrUpdateProfile(GameProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.ProcessName))
                throw new ArgumentException("Process name is required", nameof(profile));

            var existing = GetProfile(profile.ProcessName);
            if (existing != null)
            {
                existing.TargetWidth = profile.TargetWidth;
                existing.TargetHeight = profile.TargetHeight;
                existing.ExpectedAspectRatio = profile.ExpectedAspectRatio;
                existing.IsEnabled = profile.IsEnabled;
                existing.TargetRefreshRate = profile.TargetRefreshRate;
            }
            else
            {
                _profiles.Add(profile);
            }

            SaveChanges();
        }

        /// <summary>
        /// Xóa bỏ 1 profile và Save lại file.
        /// </summary>
        public bool RemoveProfile(string processName)
        {
            int removedCount = _profiles.RemoveAll(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
            if (removedCount > 0)
            {
                SaveChanges();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Đồng bộ danh sách dữ liệu Runtime (Memory) vào File JSON (Disk).
        /// </summary>
        public void SaveChanges()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(_profiles, options);
                File.WriteAllText(_profilesFilePath, jsonString);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save profiles to {_profilesFilePath}: {ex.Message}", ex);
            }

            // Báo cho UI biết danh sách đã đổi (kể cả khi do agent/MCP thực hiện).
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Parse/Đọc dữ liệu từ ổ cứng lên Memory.
        /// </summary>
        public void LoadProfiles()
        {
            try
            {
                if (!File.Exists(_profilesFilePath))
                {
                    _profiles = new List<GameProfile>();
                    return;
                }

                string jsonContent = File.ReadAllText(_profilesFilePath);
                var loadedData = JsonSerializer.Deserialize<List<GameProfile>>(jsonContent);
                
                _profiles = loadedData ?? new List<GameProfile>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load profiles from {_profilesFilePath}: {ex.Message}", ex);
            }
        }
    }
}