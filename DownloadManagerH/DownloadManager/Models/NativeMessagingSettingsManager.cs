using System;
using System.IO;
using System.Text.Json;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت تنظیمات Native Messaging و رهگیری دانلود
    /// </summary>
    public class NativeMessagingSettingsManager
    {
        private readonly ILogger _logger;
        private readonly string _settingsFilePath;
        private DownloadInterceptionSettings _settings;

        public DownloadInterceptionSettings Settings => _settings;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public NativeMessagingSettingsManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // تعیین مسیر فایل تنظیمات
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "DownloadManagerHamed");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "native-messaging-settings.json");

            // بارگذاری تنظیمات
            _settings = LoadSettings();
            
            _logger.LogInfo("Native Messaging Settings Manager initialized");
        }

        /// <summary>
        /// بارگذاری تنظیمات از فایل
        /// </summary>
        private DownloadInterceptionSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<DownloadInterceptionSettings>(json, GetJsonOptions());
                    
                    if (settings != null)
                    {
                        _logger.LogInfo("Settings loaded from file successfully");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error loading settings from file", ex);
            }

            // در صورت عدم وجود فایل یا خطا، تنظیمات پیش‌فرض را برگردان
            _logger.LogInfo("Using default settings");
            return new DownloadInterceptionSettings();
        }

        /// <summary>
        /// ذخیره تنظیمات در فایل
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, GetJsonOptions());
                File.WriteAllText(_settingsFilePath, json);
                
                _logger.LogInfo("Settings saved to file successfully");
                OnSettingsChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error saving settings to file", ex);
                throw;
            }
        }

        /// <summary>
        /// به‌روزرسانی تنظیمات
        /// </summary>
        public void UpdateSettings(DownloadInterceptionSettings newSettings)
        {
            if (newSettings == null)
                throw new ArgumentNullException(nameof(newSettings));

            _settings = newSettings;
            SaveSettings();
        }

        /// <summary>
        /// به‌روزرسانی تنظیم خاص
        /// </summary>
        public void UpdateSetting<T>(string settingName, T value)
        {
            var property = typeof(DownloadInterceptionSettings).GetProperty(settingName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(_settings, value);
                SaveSettings();
                _logger.LogDebug($"Updated setting {settingName} to {value}");
            }
            else
            {
                throw new ArgumentException($"Setting '{settingName}' not found or not writable");
            }
        }

        /// <summary>
        /// فعال/غیرفعال کردن رهگیری دانلود
        /// </summary>
        public void SetDownloadInterceptionEnabled(bool enabled)
        {
            if (_settings.EnableDownloadInterception != enabled)
            {
                _settings.EnableDownloadInterception = enabled;
                SaveSettings();
                _logger.LogInfo($"Download interception {(enabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// اضافه کردن نوع فایل به فهرست قابل رهگیری
        /// </summary>
        public void AddInterceptableFileType(string fileType)
        {
            if (string.IsNullOrWhiteSpace(fileType))
                return;

            fileType = fileType.ToLower().TrimStart('.');
            if (_settings.InterceptableFileTypes.Add(fileType))
            {
                SaveSettings();
                _logger.LogDebug($"Added file type '{fileType}' to interceptable types");
            }
        }

        /// <summary>
        /// حذف نوع فایل از فهرست قابل رهگیری
        /// </summary>
        public void RemoveInterceptableFileType(string fileType)
        {
            if (string.IsNullOrWhiteSpace(fileType))
                return;

            fileType = fileType.ToLower().TrimStart('.');
            if (_settings.InterceptableFileTypes.Remove(fileType))
            {
                SaveSettings();
                _logger.LogDebug($"Removed file type '{fileType}' from interceptable types");
            }
        }

        /// <summary>
        /// اضافه کردن دامنه به فهرست مستثنیات
        /// </summary>
        public void AddExcludedDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return;

            domain = domain.ToLower();
            if (_settings.ExcludedDomains.Add(domain))
            {
                SaveSettings();
                _logger.LogDebug($"Added domain '{domain}' to excluded domains");
            }
        }

        /// <summary>
        /// حذف دامنه از فهرست مستثنیات
        /// </summary>
        public void RemoveExcludedDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return;

            domain = domain.ToLower();
            if (_settings.ExcludedDomains.Remove(domain))
            {
                SaveSettings();
                _logger.LogDebug($"Removed domain '{domain}' from excluded domains");
            }
        }

        /// <summary>
        /// تنظیم مسیر پیش‌فرض ذخیره
        /// </summary>
        public void SetDefaultSavePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _settings.DefaultSavePath = null;
            }
            else if (Directory.Exists(path))
            {
                _settings.DefaultSavePath = path;
            }
            else
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            SaveSettings();
            _logger.LogInfo($"Default save path set to: {path ?? "default"}");
        }

        /// <summary>
        /// تنظیم حداقل اندازه فایل برای رهگیری
        /// </summary>
        public void SetMinFileSizeForInterception(long sizeInBytes)
        {
            if (sizeInBytes < 0)
                throw new ArgumentException("File size cannot be negative");

            _settings.MinFileSizeForInterception = sizeInBytes;
            SaveSettings();
            _logger.LogInfo($"Minimum file size for interception set to: {sizeInBytes} bytes");
        }

        /// <summary>
        /// بازنشانی تنظیمات به حالت پیش‌فرض
        /// </summary>
        public void ResetToDefaults()
        {
            _settings = new DownloadInterceptionSettings();
            SaveSettings();
            _logger.LogInfo("Settings reset to defaults");
        }

        /// <summary>
        /// صادرات تنظیمات به فایل
        /// </summary>
        public void ExportSettings(string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, GetJsonOptions());
                File.WriteAllText(filePath, json);
                _logger.LogInfo($"Settings exported to: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting settings to {filePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// وارد کردن تنظیمات از فایل
        /// </summary>
        public void ImportSettings(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Settings file not found: {filePath}");

                var json = File.ReadAllText(filePath);
                var importedSettings = JsonSerializer.Deserialize<DownloadInterceptionSettings>(json, GetJsonOptions());
                
                if (importedSettings != null)
                {
                    _settings = importedSettings;
                    SaveSettings();
                    _logger.LogInfo($"Settings imported from: {filePath}");
                }
                else
                {
                    throw new InvalidOperationException("Failed to deserialize settings");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error importing settings from {filePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// دریافت گزینه‌های JSON serialization
        /// </summary>
        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// رویداد تغییر تنظیمات
        /// </summary>
        protected virtual void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(_settings));
        }
    }

    /// <summary>
    /// Event Args برای تغییر تنظیمات
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {
        public DownloadInterceptionSettings Settings { get; }
        public DateTime ChangeTime { get; }

        public SettingsChangedEventArgs(DownloadInterceptionSettings settings)
        {
            Settings = settings;
            ChangeTime = DateTime.UtcNow;
        }
    }
}