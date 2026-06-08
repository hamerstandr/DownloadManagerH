using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using DownloadManagerH.Models.Logging;
using Timer = System.Timers.Timer;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// مدیریت پشتیبان‌گیری خودکار و بازیابی داده‌ها
    /// </summary>
    public class BackupManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Timer _backupTimer;
        private readonly string _backupDirectory;
        private readonly SemaphoreSlim _backupSemaphore;
        private bool _disposed = false;

        public event EventHandler<BackupEventArgs>? BackupCompleted;
        public event EventHandler<BackupEventArgs>? BackupFailed;

        public BackupManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backupDirectory = Path.Combine(Settings.DataDirectory, "Backups");
            _backupSemaphore = new SemaphoreSlim(1, 1);
            
            // ایجاد دایرکتوری پشتیبان‌گیری
            Directory.CreateDirectory(_backupDirectory);
            
            // تنظیم تایمر پشتیبان‌گیری خودکار
            _backupTimer = new Timer();
            _backupTimer.Elapsed += OnBackupTimerElapsed;
            
            _logger.LogInfo("BackupManager مقداردهی اولیه شد");
        }

        /// <summary>
        /// فعال‌سازی پشتیبان‌گیری خودکار
        /// </summary>
        private void EnableAutomaticBackup(TimeSpan interval)
        {
            if (interval.TotalMinutes < 1)
            {
                throw new ArgumentException("فاصله زمانی پشتیبان‌گیری نمی‌تواند کمتر از یک دقیقه باشد");
            }

            _backupTimer.Interval = interval.TotalMilliseconds;
            _backupTimer.Start();
            
            _logger.LogInfo($"پشتیبان‌گیری خودکار فعال شد با فاصله {interval.TotalHours:F1} ساعت");
        }

        /// <summary>
        /// غیرفعال‌سازی پشتیبان‌گیری خودکار
        /// </summary>
        public void DisableAutomaticBackup()
        {
            _backupTimer.Stop();
            _logger.LogInfo("پشتیبان‌گیری خودکار غیرفعال شد");
        }

        /// <summary>
        /// ایجاد پشتیبان کامل
        /// </summary>
        public async Task<BackupResult> CreateBackupAsync(string? customPath = null, bool includeDownloadFiles = false)
        {
            await _backupSemaphore.WaitAsync();
            
            try
            {
                var backupId = Guid.NewGuid().ToString("N")[..8];
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"DownloadManager_Backup_{timestamp}_{backupId}.zip";
                var backupPath = customPath ?? Path.Combine(_backupDirectory, backupFileName);

                _logger.LogInfo($"شروع ایجاد پشتیبان: {backupFileName}");

                var backupInfo = new BackupInfo
                {
                    Id = backupId,
                    CreatedAt = DateTime.Now,
                    Version = GetApplicationVersion(),
                    IncludesDownloadFiles = includeDownloadFiles,
                    FilePath = backupPath
                };

                // ایجاد فایل پشتیبان
                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // اضافه کردن فایل‌های تنظیمات
                     AddSettingsToBackup(archive);
                    
                    // اضافه کردن تاریخچه دانلود
                    AddHistoryToBackup(archive);
                    
                    // اضافه کردن لاگ‌ها
                    AddLogsToBackup(archive);
                    
                    // اضافه کردن فایل‌های دانلود شده (اختیاری)
                    if (includeDownloadFiles)
                    {
                        await AddDownloadFilesToBackup(archive);
                    }
                    
                    // اضافه کردن اطلاعات پشتیبان
                    await AddBackupInfoToArchive(archive, backupInfo);
                }

                // محاسبه checksum
                backupInfo.Checksum = await CalculateFileChecksumAsync(backupPath);
                backupInfo.Size = new FileInfo(backupPath).Length;

                // ذخیره اطلاعات پشتیبان
                await SaveBackupInfoAsync(backupInfo);

                var result = new BackupResult
                {
                    Success = true,
                    BackupInfo = backupInfo,
                    Message = "پشتیبان‌گیری با موفقیت انجام شد"
                };

                BackupCompleted?.Invoke(this, new BackupEventArgs { Result = result });
                _logger.LogInfo($"پشتیبان‌گیری کامل شد: {backupPath}");

                return result;
            }
            catch (Exception ex)
            {
                var result = new BackupResult
                {
                    Success = false,
                    Message = $"خطا در ایجاد پشتیبان: {ex.Message}"
                };

                BackupFailed?.Invoke(this, new BackupEventArgs { Result = result });
                _logger.LogError("خطا در ایجاد پشتیبان", ex);

                return result;
            }
            finally
            {
                _backupSemaphore.Release();
            }
        }

        /// <summary>
        /// بازیابی از پشتیبان
        /// </summary>
        public async Task<RestoreResult> RestoreBackupAsync(string backupPath, bool overwriteExisting = false)
        {
            await _backupSemaphore.WaitAsync();
            
            try
            {
                if (!File.Exists(backupPath))
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Message = "فایل پشتیبان یافت نشد"
                    };
                }

                _logger.LogInfo($"شروع بازیابی از پشتیبان: {backupPath}");

                // اعتبارسنجی پشتیبان
                var validationResult = await ValidateBackupAsync(backupPath);
                if (!validationResult.IsValid)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Message = $"پشتیبان نامعتبر است: {validationResult.ErrorMessage}"
                    };
                }

                // ایجاد پشتیبان از وضعیت فعلی قبل از بازیابی
                var currentBackupResult = await CreateBackupAsync(
                    Path.Combine(_backupDirectory, $"PreRestore_{DateTime.Now:yyyyMMdd_HHmmss}.zip"));

                if (!currentBackupResult.Success)
                {
                    _logger.LogWarning("نتوانست پشتیبان از وضعیت فعلی ایجاد کند");
                }

                var restoredItems = new List<string>();

                // استخراج و بازیابی فایل‌ها
                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    // بازیابی تنظیمات
                    if (RestoreSettingsFromBackup(archive, overwriteExisting))
                    {
                        restoredItems.Add("تنظیمات");
                    }

                    // بازیابی تاریخچه
                    if (RestoreHistoryFromBackup(archive, overwriteExisting))
                    {
                        restoredItems.Add("تاریخچه دانلود");
                    }

                    // بازیابی لاگ‌ها
                    if (RestoreLogsFromBackup(archive, overwriteExisting))
                    {
                        restoredItems.Add("لاگ‌ها");
                    }

                    // بازیابی فایل‌های دانلود شده
                    if (await RestoreDownloadFilesFromBackup(archive, overwriteExisting))
                    {
                        restoredItems.Add("فایل‌های دانلود شده");
                    }
                }

                var result = new RestoreResult
                {
                    Success = true,
                    RestoredItems = restoredItems,
                    Message = $"بازیابی موفقیت‌آمیز بود. موارد بازیابی شده: {string.Join(", ", restoredItems)}"
                };

                _logger.LogInfo($"بازیابی کامل شد: {restoredItems.Count} مورد بازیابی شد");
                return result;
            }
            catch (Exception ex)
            {
                var result = new RestoreResult
                {
                    Success = false,
                    Message = $"خطا در بازیابی: {ex.Message}"
                };

                _logger.LogError("خطا در بازیابی پشتیبان", ex);
                return result;
            }
            finally
            {
                _backupSemaphore.Release();
            }
        }

        /// <summary>
        /// اعتبارسنجی فایل پشتیبان
        /// </summary>
        public async Task<BackupValidationResult> ValidateBackupAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    return new BackupValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "فایل پشتیبان وجود ندارد"
                    };
                }

                // بررسی فرمت ZIP
                try
                {
                    using var archive = ZipFile.OpenRead(backupPath);
                    
                    // بررسی وجود فایل اطلاعات پشتیبان
                    var backupInfoEntry = archive.GetEntry("backup_info.json");
                    if (backupInfoEntry == null)
                    {
                        return new BackupValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "فایل اطلاعات پشتیبان یافت نشد"
                        };
                    }

                    // خواندن اطلاعات پشتیبان
                    using var stream = backupInfoEntry.Open();
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    var backupInfo = JsonSerializer.Deserialize<BackupInfo>(json);

                    if (backupInfo == null)
                    {
                        return new BackupValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "اطلاعات پشتیبان نامعتبر است"
                        };
                    }

                    // بررسی checksum
                    if (!string.IsNullOrEmpty(backupInfo.Checksum))
                    {
                        var currentChecksum = await CalculateFileChecksumAsync(backupPath);
                        if (currentChecksum != backupInfo.Checksum)
                        {
                            return new BackupValidationResult
                            {
                                IsValid = false,
                                ErrorMessage = "checksum فایل پشتیبان مطابقت ندارد"
                            };
                        }
                    }

                    return new BackupValidationResult
                    {
                        IsValid = true,
                        BackupInfo = backupInfo
                    };
                }
                catch (InvalidDataException)
                {
                    return new BackupValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "فایل پشتیبان فاسد است"
                    };
                }
            }
            catch (Exception ex)
            {
                return new BackupValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"خطا در اعتبارسنجی: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// دریافت لیست پشتیبان‌های موجود
        /// </summary>
        public async Task<List<BackupInfo>> GetAvailableBackupsAsync()
        {
            try
            {
                var backups = new List<BackupInfo>();
                var backupFiles = Directory.GetFiles(_backupDirectory, "*.zip");

                foreach (var backupFile in backupFiles)
                {
                    var validationResult = await ValidateBackupAsync(backupFile);
                    if (validationResult.IsValid && validationResult.BackupInfo != null)
                    {
                        validationResult.BackupInfo.FilePath = backupFile;
                        backups.Add(validationResult.BackupInfo);
                    }
                }

                return backups.OrderByDescending(b => b.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در دریافت لیست پشتیبان‌ها", ex);
                return new List<BackupInfo>();
            }
        }

        /// <summary>
        /// حذف پشتیبان‌های قدیمی
        /// </summary>
        public async Task<int> CleanupOldBackupsAsync(int keepCount = 10, TimeSpan? olderThan = null)
        {
            try
            {
                var backups = await GetAvailableBackupsAsync();
                var backupsToDelete = new List<BackupInfo>();

                // حذف بر اساس تعداد
                if (backups.Count > keepCount)
                {
                    backupsToDelete.AddRange(backups.Skip(keepCount));
                }

                // حذف بر اساس تاریخ
                if (olderThan.HasValue)
                {
                    var cutoffDate = DateTime.Now - olderThan.Value;
                    backupsToDelete.AddRange(backups.Where(b => b.CreatedAt < cutoffDate));
                }

                // حذف تکراری‌ها
                backupsToDelete = backupsToDelete.Distinct().ToList();

                var deletedCount = 0;
                foreach (var backup in backupsToDelete)
                {
                    try
                    {
                        if (File.Exists(backup.FilePath))
                        {
                            File.Delete(backup.FilePath);
                            deletedCount++;
                            _logger.LogInfo($"پشتیبان قدیمی حذف شد: {Path.GetFileName(backup.FilePath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"نتوانست پشتیبان را حذف کند: {backup.FilePath}", ex);
                    }
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در پاک‌سازی پشتیبان‌های قدیمی", ex);
                return 0;
            }
        }

        #region Private Methods

        private async void OnBackupTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                await CreateBackupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در پشتیبان‌گیری خودکار", ex);
            }
        }

        private static void AddSettingsToBackup(ZipArchive archive)
        {
            // اضافه کردن فایل‌های تنظیمات
            var settingsFiles = new[]
            {
                "settings.json",
                "user_preferences.json",
                "groups.json"
            };

            foreach (var settingsFile in settingsFiles)
            {
                var filePath = Path.Combine(Settings.DataDirectory, settingsFile);
                if (File.Exists(filePath))
                {
                    archive.CreateEntryFromFile(filePath, $"settings/{settingsFile}");
                }
            }
        }

        private static void AddHistoryToBackup(ZipArchive archive)
        {
            var historyFile = Path.Combine(Settings.DataDirectory, "download_history.json");
            if (File.Exists(historyFile))
            {
                archive.CreateEntryFromFile(historyFile, "data/download_history.json");
            }
        }

        private static void AddLogsToBackup(ZipArchive archive)
        {
            var logsDirectory = Path.Combine(Settings.DataDirectory, "logs");
            if (Directory.Exists(logsDirectory))
            {
                var logFiles = Directory.GetFiles(logsDirectory, "*.log");
                foreach (var logFile in logFiles.Take(5)) // فقط 5 فایل لاگ اخیر
                {
                    var fileName = Path.GetFileName(logFile);
                    archive.CreateEntryFromFile(logFile, $"logs/{fileName}");
                }
            }
        }

        private static async Task AddDownloadFilesToBackup(ZipArchive archive)
        {
            // این متد می‌تواند فایل‌های دانلود شده را نیز شامل شود
            // برای جلوگیری از بزرگ شدن بیش از حد پشتیبان، فعلاً خالی است
            await Task.CompletedTask;
        }

        private static async Task AddBackupInfoToArchive(ZipArchive archive, BackupInfo backupInfo)
        {
            var entry = archive.CreateEntry("backup_info.json");
            using var stream = entry.Open();
            var json = JsonSerializer.Serialize(backupInfo, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            await stream.WriteAsync(bytes);
        }

        private bool RestoreSettingsFromBackup(ZipArchive archive, bool overwrite)
        {
            try
            {
                var settingsEntries = archive.Entries.Where(e => e.FullName.StartsWith("settings/"));
                foreach (var entry in settingsEntries)
                {
                    var fileName = Path.GetFileName(entry.FullName);
                    var targetPath = Path.Combine(Settings.DataDirectory, fileName);

                    if (!overwrite && File.Exists(targetPath))
                        continue;

                    entry.ExtractToFile(targetPath, overwrite);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در بازیابی تنظیمات", ex);
                return false;
            }
        }

        private bool RestoreHistoryFromBackup(ZipArchive archive, bool overwrite)
        {
            try
            {
                var historyEntry = archive.GetEntry("data/download_history.json");
                if (historyEntry != null)
                {
                    var targetPath = Path.Combine(Settings.DataDirectory, "download_history.json");
                    if (overwrite || !File.Exists(targetPath))
                    {
                        historyEntry.ExtractToFile(targetPath, overwrite);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در بازیابی تاریخچه", ex);
                return false;
            }
        }

        private bool RestoreLogsFromBackup(ZipArchive archive, bool overwrite)
        {
            try
            {
                var logsDirectory = Path.Combine(Settings.DataDirectory, "logs");
                Directory.CreateDirectory(logsDirectory);

                var logEntries = archive.Entries.Where(e => e.FullName.StartsWith("logs/"));
                foreach (var entry in logEntries)
                {
                    var fileName = Path.GetFileName(entry.FullName);
                    var targetPath = Path.Combine(logsDirectory, fileName);

                    if (!overwrite && File.Exists(targetPath))
                        continue;

                    entry.ExtractToFile(targetPath, overwrite);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در بازیابی لاگ‌ها", ex);
                return false;
            }
        }

        private static async Task<bool> RestoreDownloadFilesFromBackup(ZipArchive archive, bool overwrite)
        {
            // پیاده‌سازی بازیابی فایل‌های دانلود شده
            await Task.CompletedTask;
            return false;
        }

        private static async Task<string> CalculateFileChecksumAsync(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await Task.Run(() => md5.ComputeHash(stream));
            return Convert.ToHexString(hash);
        }

        private async Task SaveBackupInfoAsync(BackupInfo backupInfo)
        {
            var infoPath = Path.Combine(_backupDirectory, $"{backupInfo.Id}_info.json");
            var json = JsonSerializer.Serialize(backupInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(infoPath, json);
        }

        private static string GetApplicationVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _backupTimer?.Dispose();
                _backupSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }

    #region Data Models

    public class BackupInfo
    {
        public string Id { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Version { get; set; } = "1";
        public bool IncludesDownloadFiles { get; set; }
        public string FilePath { get; set; } = "";
        public string Checksum { get; set; } = "";
        public long Size { get; set; }
        public string Description { get; set; } = "";

        public string SizeFormatted => FormatFileSize(Size);

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:F2} {sizes[order]}";
        }
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public BackupInfo BackupInfo { get; set; }= new BackupInfo();
        public string Message { get; set; } = "";
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public List<string> RestoredItems { get; set; } = [];
        public string Message { get; set; } = "";
    }

    public class BackupValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
        public BackupInfo BackupInfo { get; set; }=new BackupInfo();
    }

    public class BackupEventArgs : EventArgs
    {
        public BackupResult? Result { get; set; }
    }

    #endregion
}