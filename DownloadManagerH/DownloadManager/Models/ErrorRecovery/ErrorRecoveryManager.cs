using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models.ErrorRecovery
{
    /// <summary>
    /// مدیر بازیابی خطا برای انواع مختلف خطاها
    /// </summary>
    public class ErrorRecoveryManager
    {
        private readonly ILogger _logger;
        private readonly RetryPolicy _networkRetryPolicy;
        private readonly RetryPolicy _fileRetryPolicy;

        /// <summary>
        /// سازنده ErrorRecoveryManager
        /// </summary>
        /// <param name="logger">لاگر</param>
        public ErrorRecoveryManager(ILogger? logger = null)
        {
            _logger = logger ?? LoggerFactory.GetDefaultLogger();
            _networkRetryPolicy = RetryPolicy.ForNetwork(_logger);
            _fileRetryPolicy = RetryPolicy.ForFileOperations(_logger);
        }

        /// <summary>
        /// بازیابی از خطاهای شبکه
        /// </summary>
        /// <param name="item">آیتم دانلود</param>
        /// <param name="error">خطای رخ داده</param>
        /// <returns>true اگر بازیابی موفق باشد</returns>
        public async Task<bool> RecoverFromNetworkError(DownloadItem item, Exception error)
        {
            try
            {
                _logger.LogInfo($"شروع بازیابی از خطای شبکه برای {item.FileName}", new { Error = error.Message });

                // تشخیص نوع خطای شبکه
                var errorType = ClassifyNetworkError(error);
                _logger.LogDebug($"نوع خطای شبکه: {errorType}");

                switch (errorType)
                {
                    case NetworkErrorType.Timeout:
                        return await RecoverFromTimeout(item);
                    
                    case NetworkErrorType.ConnectionRefused:
                        return await RecoverFromConnectionRefused(item);
                    
                    case NetworkErrorType.DnsFailure:
                        return await RecoverFromDnsFailure(item);
                    
                    case NetworkErrorType.ServerError:
                        return await RecoverFromServerError(item);
                    
                    case NetworkErrorType.Unauthorized:
                        _logger.LogWarning($"خطای احراز هویت - بازیابی غیرممکن: {item.FileName}");
                        return false;
                    
                    default:
                        return await RecoverFromGenericNetworkError(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در بازیابی از خطای شبکه برای {item.FileName}", ex);
                return false;
            }
        }

        /// <summary>
        /// بازیابی از خطاهای سیستم فایل
        /// </summary>
        /// <param name="item">آیتم دانلود</param>
        /// <param name="error">خطای رخ داده</param>
        /// <returns>true اگر بازیابی موفق باشد</returns>
        public async Task<bool> RecoverFromFileSystemError(DownloadItem item, Exception error)
        {
            try
            {
                _logger.LogInfo($"شروع بازیابی از خطای سیستم فایل برای {item.FileName}", new { Error = error.Message });

                var errorType = ClassifyFileSystemError(error);
                _logger.LogDebug($"نوع خطای سیستم فایل: {errorType}");

                switch (errorType)
                {
                    case FileSystemErrorType.DiskFull:
                        return RecoverFromDiskFull(item);
                    
                    case FileSystemErrorType.AccessDenied:
                        return RecoverFromAccessDenied(item);
                    
                    case FileSystemErrorType.PathTooLong:
                        return RecoverFromPathTooLong(item);
                    
                    case FileSystemErrorType.FileInUse:
                        return await RecoverFromFileInUse(item);
                    
                    case FileSystemErrorType.DirectoryNotFound:
                        return RecoverFromDirectoryNotFound(item);
                    
                    default:
                        return await RecoverFromGenericFileSystemError(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در بازیابی از خطای سیستم فایل برای {item.FileName}", ex);
                return false;
            }
        }

        /// <summary>
        /// مدیریت خطاهای حیاتی
        /// </summary>
        /// <param name="error">خطای حیاتی</param>
        /// <param name="context">زمینه خطا</param>
        public void HandleCriticalError(Exception error, string context)
        {
            _logger.LogError($"خطای حیاتی در {context}", error);
            
            // اطلاع‌رسانی به کاربر
            ReportErrorToUser($"خطای حیاتی رخ داده است: {error.Message}", ErrorSeverity.Critical);
            
            // ذخیره اطلاعات خطا برای تحلیل
            SaveCrashReport(error, context);
        }

        /// <summary>
        /// گزارش خطا به کاربر
        /// </summary>
        /// <param name="message">پیام خطا</param>
        /// <param name="severity">شدت خطا</param>
        public void ReportErrorToUser(string message, ErrorSeverity severity)
        {
            try
            {
                // نمایش پیام به کاربر بر اساس شدت
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var messageType = severity switch
                    {
                        ErrorSeverity.Low => Windows.Dialog.CustomMessageBoxType.Success,
                        ErrorSeverity.Medium => Windows.Dialog.CustomMessageBoxType.Warning,
                        ErrorSeverity.High => Windows.Dialog.CustomMessageBoxType.Error,
                        ErrorSeverity.Critical => Windows.Dialog.CustomMessageBoxType.Error,
                        _ => Windows.Dialog.CustomMessageBoxType.Success
                    };

                    Windows.Dialog.CustomMessageBox.Show(message, "خطا", messageType);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در نمایش پیام خطا به کاربر", ex);
            }
        }

        #region Private Methods - Network Error Recovery

        private NetworkErrorType ClassifyNetworkError(Exception error)
        {
            return error switch
            {
                TaskCanceledException => NetworkErrorType.Timeout,
                HttpRequestException httpEx when httpEx.Message.Contains("timeout") => NetworkErrorType.Timeout,
                HttpRequestException httpEx when httpEx.Message.Contains("refused") => NetworkErrorType.ConnectionRefused,
                HttpRequestException httpEx when httpEx.Message.Contains("DNS") => NetworkErrorType.DnsFailure,
                HttpRequestException httpEx when httpEx.Data.Contains("StatusCode") => 
                    (HttpStatusCode?)httpEx.Data["StatusCode"] >= HttpStatusCode.InternalServerError ? 
                    NetworkErrorType.ServerError : NetworkErrorType.ClientError,
                _ => NetworkErrorType.Unknown
            };
        }

        private async Task<bool> RecoverFromTimeout(DownloadItem item)
        {
            _logger.LogInfo($"بازیابی از تایم‌اوت برای {item.FileName}");
            
            // افزایش timeout و تلاش مجدد
            await Task.Delay(TimeSpan.FromSeconds(5));
            return true; // اجازه تلاش مجدد
        }

        private async Task<bool> RecoverFromConnectionRefused(DownloadItem item)
        {
            _logger.LogInfo($"بازیابی از رد اتصال برای {item.FileName}");
            
            // تأخیر بیشتر برای اتصال مجدد
            await Task.Delay(TimeSpan.FromSeconds(10));
            return true;
        }

        private async Task<bool> RecoverFromDnsFailure(DownloadItem item)
        {
            _logger.LogInfo($"بازیابی از خطای DNS برای {item.FileName}");
            
            // تأخیر برای حل مشکل DNS
            await Task.Delay(TimeSpan.FromSeconds(15));
            return true;
        }

        private async Task<bool> RecoverFromServerError(DownloadItem item)
        {
            _logger.LogInfo($"بازیابی از خطای سرور برای {item.FileName}");
            
            // تأخیر طولانی‌تر برای خطاهای سرور
            await Task.Delay(TimeSpan.FromMinutes(1));
            return true;
        }

        private async Task<bool> RecoverFromGenericNetworkError(DownloadItem item)
        {
            _logger.LogInfo($"بازیابی عمومی از خطای شبکه برای {item.FileName}");
            
            await Task.Delay(TimeSpan.FromSeconds(3));
            return true;
        }

        #endregion

        #region Private Methods - File System Error Recovery

        private FileSystemErrorType ClassifyFileSystemError(Exception error)
        {
            return error switch
            {
                DirectoryNotFoundException => FileSystemErrorType.DirectoryNotFound,
                UnauthorizedAccessException => FileSystemErrorType.AccessDenied,
                IOException ioEx when ioEx.Message.Contains("disk") => FileSystemErrorType.DiskFull,
                IOException ioEx when ioEx.Message.Contains("being used") => FileSystemErrorType.FileInUse,
                PathTooLongException => FileSystemErrorType.PathTooLong,
                _ => FileSystemErrorType.Unknown
            };
        }

        private bool RecoverFromDiskFull(DownloadItem item)
        {
            _logger.LogWarning($"دیسک پر است برای {item.FileName}");

            // بررسی فضای خالی
            var drive = new DriveInfo(Path.GetPathRoot(item.SavePath)+"");
            var freeSpace = drive.AvailableFreeSpace;

            if (freeSpace < item.TotalBytes)
            {
                ReportErrorToUser($"فضای کافی در دیسک وجود ندارد. فضای مورد نیاز: {item.TotalBytes / (1024 * 1024)} مگابایت", ErrorSeverity.High);
                return false;
            }

            return true;
        }

        private bool RecoverFromAccessDenied(DownloadItem item)
        {
            _logger.LogWarning($"دسترسی رد شده برای {item.FileName}");

            // تلاش برای تغییر مسیر به پوشه موقت
            var tempPath = Path.Combine(Path.GetTempPath(), "DownloadManager");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            item.SavePath = tempPath;
            _logger.LogInfo($"مسیر ذخیره تغییر یافت به: {tempPath}");

            return true;
        }

        private bool RecoverFromPathTooLong(DownloadItem item)
        {
            _logger.LogWarning($"مسیر فایل خیلی طولانی است برای {item.FileName}");

            // کوتاه کردن نام فایل
            var extension = Path.GetExtension(item.FileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(item.FileName);

            if (nameWithoutExt.Length > 50)
            {
                nameWithoutExt = nameWithoutExt.Substring(0, 50);
                item.FileName = nameWithoutExt + extension;
                _logger.LogInfo($"نام فایل کوتاه شد: {item.FileName}");
                return true;
            }

            return false;
        }

        private async Task<bool> RecoverFromFileInUse(DownloadItem item)
        {
            _logger.LogInfo($"فایل در حال استفاده است: {item.FileName}");
            
            // تأخیر و تلاش مجدد
            await Task.Delay(TimeSpan.FromSeconds(5));
            return true;
        }

        private bool RecoverFromDirectoryNotFound(DownloadItem item)
        {
            _logger.LogInfo($"پوشه وجود ندارد، ایجاد می‌شود: {item.SavePath}");

            try
            {
                Directory.CreateDirectory(item.SavePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در ایجاد پوشه: {item.SavePath}", ex);
                return false;
            }
        }

        private async Task<bool> RecoverFromGenericFileSystemError(DownloadItem item)
        {
            _logger.LogInfo($"بازیابی عمومی از خطای سیستم فایل برای {item.FileName}");
            
            await Task.Delay(TimeSpan.FromSeconds(2));
            return true;
        }

        #endregion

        #region Private Methods - Utilities

        private void SaveCrashReport(Exception error, string context)
        {
            try
            {
                var crashReportPath = Path.Combine(Settings.DataDirectory, "CrashReports");
                if (!Directory.Exists(crashReportPath))
                {
                    Directory.CreateDirectory(crashReportPath);
                }

                var fileName = $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(crashReportPath, fileName);

                var report = $"Crash Report - {DateTime.Now}\n" +
                           $"Context: {context}\n" +
                           $"Exception: {error.GetType().Name}\n" +
                           $"Message: {error.Message}\n" +
                           $"StackTrace:\n{error.StackTrace}\n";

                File.WriteAllText(filePath, report);
                _logger.LogInfo($"گزارش خطا ذخیره شد: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در ذخیره گزارش خطا", ex);
            }
        }

        #endregion
    }

    /// <summary>
    /// انواع خطاهای شبکه
    /// </summary>
    public enum NetworkErrorType
    {
        Unknown,
        Timeout,
        ConnectionRefused,
        DnsFailure,
        ServerError,
        ClientError,
        Unauthorized
    }

    /// <summary>
    /// انواع خطاهای سیستم فایل
    /// </summary>
    public enum FileSystemErrorType
    {
        Unknown,
        DiskFull,
        AccessDenied,
        PathTooLong,
        FileInUse,
        DirectoryNotFound
    }

    /// <summary>
    /// شدت خطا
    /// </summary>
    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}