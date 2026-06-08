using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// پیاده‌سازی لاگر فایلی با قابلیت چرخش خودکار
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private readonly LoggingConfiguration _config;
        private readonly SemaphoreSlim _writeSemaphore;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        /// <summary>
        /// سازنده FileLogger
        /// </summary>
        /// <param name="config">تنظیمات لاگ‌گیری</param>
        public FileLogger(LoggingConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _writeSemaphore = new SemaphoreSlim(1, 1);
            
            // اطمینان از وجود پوشه لاگ
            _config.EnsureLogDirectoryExists();
            
            // تایمر پاک‌سازی هر ساعت
            _cleanupTimer = new Timer(CleanupOldLogs, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        /// <summary>
        /// ثبت پیام اشکال‌زدایی
        /// </summary>
        public void LogDebug(string message, object? context = null)
        {
            WriteLog(LogLevel.Debug, message, "Debug", context);
        }

        /// <summary>
        /// ثبت پیام اطلاعاتی
        /// </summary>
        public void LogInfo(string message, object? context = null)
        {
            WriteLog(LogLevel.Info, message, "Info", context);
        }

        /// <summary>
        /// ثبت پیام هشدار
        /// </summary>
        public void LogWarning(string message, object? context = null)
        {
            WriteLog(LogLevel.Warning, message, "Warning", context);
        }

        /// <summary>
        /// ثبت پیام خطا
        /// </summary>
        public void LogError(string message, Exception? exception = null, object? context = null)
        {
            var entry = new LogEntry(LogLevel.Error, message, "Error")
            {
                Exception = exception
            };
            entry.SetContext(context);
            
            WriteLogEntry(entry);
        }

        /// <summary>
        /// ثبت رویداد دانلود
        /// </summary>
        public void LogDownloadEvent(DownloadItem item, string eventType, object? additionalData = null)
        {
            if (!_config.EnableDownloadEventLogging)
                return;

            var contextData = new
            {
                DownloadId = item?.Url?.GetHashCode(),
                FileName = item?.FileName,
                Status = item?.Status.ToString(),
                Progress = item?.Progress,
                EventType = eventType,
                AdditionalData = additionalData
            };

            WriteLog(LogLevel.Info, $"رویداد دانلود: {eventType}", "Download", contextData);
        }

        /// <summary>
        /// دریافت لاگ‌های فایل (پیاده‌سازی ساده)
        /// </summary>
        public async Task<IEnumerable<LogEntry>> GetLogsAsync(LogLevel minLevel, DateTime? from = null, DateTime? to = null)
        {
            var logs = new List<LogEntry>();
            
            try
            {
                await _writeSemaphore.WaitAsync();
                
                var logFiles = GetLogFiles(from, to);
                
                foreach (var file in logFiles)
                {
                    var fileLogs = await ReadLogEntriesFromFile(file, minLevel, from, to);
                    logs.AddRange(fileLogs);
                }
                
                return logs.OrderBy(l => l.Timestamp);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        /// <summary>
        /// نوشتن لاگ با پارامترهای ساده
        /// </summary>
        private void WriteLog(LogLevel level, string message, string category, object context = null)
        {
            if (level < _config.MinimumLevel)
                return;

            var entry = new LogEntry(level, message, category);
            entry.SetContext(context);
            
            WriteLogEntry(entry);
        }

        /// <summary>
        /// نوشتن ورودی لاگ در فایل
        /// </summary>
        private void WriteLogEntry(LogEntry entry)
        {
            if (!_config.EnableFileLogging)
                return;

            Task.Run(async () =>
            {
                try
                {
                    await _writeSemaphore.WaitAsync();
                    
                    var logFilePath = _config.GetLogFilePath(entry.Timestamp);
                    
                    // بررسی نیاز به چرخش فایل
                    RotateLogFileIfNeeded(logFilePath);
                    
                    // نوشتن لاگ
                    var logText = FormatLogEntry(entry);
                    await File.AppendAllTextAsync(logFilePath, logText + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    // در صورت خطا در نوشتن لاگ، سعی می‌کنیم در کنسول بنویسیم
                    Console.WriteLine($"خطا در نوشتن لاگ: {ex.Message}");
                }
                finally
                {
                    _writeSemaphore.Release();
                }
            });
        }

        /// <summary>
        /// فرمت‌بندی ورودی لاگ برای نوشتن در فایل
        /// </summary>
        private string FormatLogEntry(LogEntry entry)
        {
            var sb = new StringBuilder();
            
            // زمان و سطح
            sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}]");
            sb.Append($"[{GetLevelText(entry.Level)}]");
            sb.Append($"[{entry.Category}]");
            sb.Append($" {entry.Message}");
            
            // اطلاعات زمینه‌ای
            if (!string.IsNullOrEmpty(entry.Context))
            {
                sb.Append($" | Context: {entry.Context}");
            }
            
            // خصوصیات اضافی
            if (entry.Properties.Any())
            {
                sb.Append(" | Properties: ");
                sb.Append(string.Join(", ", entry.Properties.Select(p => $"{p.Key}={p.Value}")));
            }
            
            // جزئیات استثنا
            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append($"Exception: {entry.Exception.Message}");
                sb.AppendLine();
                sb.Append($"StackTrace: {entry.Exception.StackTrace}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// دریافت متن سطح لاگ
        /// </summary>
        private string GetLevelText(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                _ => level.ToString().PadRight(5)
            };
        }

        /// <summary>
        /// چرخش فایل لاگ در صورت نیاز
        /// </summary>
        private void RotateLogFileIfNeeded(string logFilePath)
        {
            try
            {
                if (!File.Exists(logFilePath))
                    return;

                var fileInfo = new FileInfo(logFilePath);
                if (fileInfo.Length < _config.MaxFileSize)
                    return;

                // ایجاد نام فایل جدید با شماره
                var directory = Path.GetDirectoryName(logFilePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(logFilePath);
                var extension = Path.GetExtension(logFilePath);

                var counter = 1;
                string newFileName;
                string newFilePath;

                do
                {
                    newFileName = $"{fileNameWithoutExt}_{counter:D3}{extension}";
                    newFilePath = Path.Combine(directory, newFileName);
                    counter++;
                } while (File.Exists(newFilePath));

                // انتقال فایل فعلی
                File.Move(logFilePath, newFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در چرخش فایل لاگ: {ex.Message}");
            }
        }

        /// <summary>
        /// پاک‌سازی فایل‌های لاگ قدیمی
        /// </summary>
        private void CleanupOldLogs(object state)
        {
            try
            {
                if (!Directory.Exists(_config.LogDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-_config.RetentionDays);
                var logFiles = Directory.GetFiles(_config.LogDirectory, "*.log")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTime < cutoffDate)
                    .OrderBy(f => f.CreationTime)
                    .ToList();

                // حذف فایل‌های قدیمی
                foreach (var file in logFiles)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // نادیده گرفتن خطاهای حذف فایل
                    }
                }

                // حذف فایل‌های اضافی بر اساس تعداد
                var allLogFiles = Directory.GetFiles(_config.LogDirectory, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(_config.MaxFileCount)
                    .ToList();

                foreach (var file in allLogFiles)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // نادیده گرفتن خطاهای حذف فایل
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در پاک‌سازی لاگ‌ها: {ex.Message}");
            }
        }

        /// <summary>
        /// دریافت فایل‌های لاگ در بازه زمانی مشخص
        /// </summary>
        private IEnumerable<string> GetLogFiles(DateTime? from, DateTime? to)
        {
            if (!Directory.Exists(_config.LogDirectory))
                return Enumerable.Empty<string>();

            var files = Directory.GetFiles(_config.LogDirectory, "*.log");
            
            if (from.HasValue || to.HasValue)
            {
                files = files.Where(f =>
                {
                    var fileInfo = new FileInfo(f);
                    var fileDate = fileInfo.CreationTime.Date;
                    
                    if (from.HasValue && fileDate < from.Value.Date)
                        return false;
                    
                    if (to.HasValue && fileDate > to.Value.Date)
                        return false;
                    
                    return true;
                }).ToArray();
            }
            
            return files.OrderBy(f => new FileInfo(f).CreationTime);
        }

        /// <summary>
        /// خواندن ورودی‌های لاگ از فایل
        /// </summary>
        private async Task<IEnumerable<LogEntry>> ReadLogEntriesFromFile(string filePath, LogLevel minLevel, DateTime? from, DateTime? to)
        {
            var entries = new List<LogEntry>();
            
            try
            {
                var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
                
                foreach (var line in lines)
                {
                    var entry = ParseLogLine(line);
                    if (entry != null && 
                        entry.Level >= minLevel &&
                        (!from.HasValue || entry.Timestamp >= from.Value) &&
                        (!to.HasValue || entry.Timestamp <= to.Value))
                    {
                        entries.Add(entry);
                    }
                }
            }
            catch
            {
                // نادیده گرفتن خطاهای خواندن فایل
            }
            
            return entries;
        }

        /// <summary>
        /// تجزیه خط لاگ به ورودی لاگ
        /// </summary>
        private LogEntry ParseLogLine(string line)
        {
            try
            {
                // پارس ساده - می‌توان پیچیده‌تر کرد
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("["))
                    return null;

                var parts = line.Split(new[] { "][" }, StringSplitOptions.None);
                if (parts.Length < 4)
                    return null;

                // استخراج زمان
                var timestampStr = parts[0].Substring(1);
                if (!DateTime.TryParse(timestampStr, out var timestamp))
                    return null;

                // استخراج سطح
                var levelStr = parts[1].Trim();
                var level = levelStr switch
                {
                    "DEBUG" => LogLevel.Debug,
                    "INFO" => LogLevel.Info,
                    "WARN" => LogLevel.Warning,
                    "ERROR" => LogLevel.Error,
                    _ => LogLevel.Info
                };

                // استخراج دسته‌بندی
                var category = parts[2];

                // استخراج پیام
                var messageStart = parts[3].IndexOf("] ");
                var message = messageStart >= 0 ? parts[3].Substring(messageStart + 2) : parts[3];

                return new LogEntry(level, message, category)
                {
                    Timestamp = timestamp
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// آزادسازی منابع
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _writeSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}