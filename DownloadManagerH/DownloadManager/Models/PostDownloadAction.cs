using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// کلاس پایه برای اقدامات پس از دانلود
    /// </summary>
    public abstract class PostDownloadAction
    {
        /// <summary>
        /// نام اقدام
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// توضیحات اقدام
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// فعال بودن اقدام
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// اجرای اقدام
        /// </summary>
        /// <param name="item">آیتم دانلود</param>
        /// <param name="logger">لاگر</param>
        /// <returns>نتیجه اجرا</returns>
        public abstract Task<bool> ExecuteAsync(DownloadItem item, ILogger? logger = null);
    }

    /// <summary>
    /// انتقال فایل به مسیر دیگر
    /// </summary>
    public class MoveFileAction : PostDownloadAction
    {
        public override string Name => "انتقال فایل";
        public override string Description => "انتقال فایل دانلود شده به مسیر مشخص";

        /// <summary>
        /// مسیر مقصد
        /// </summary>
        public string DestinationPath { get; set; }

        /// <summary>
        /// جایگزینی فایل موجود
        /// </summary>
        public bool OverwriteExisting { get; set; } = false;

        public MoveFileAction(string destinationPath)
        {
            DestinationPath = destinationPath;
        }

        public override async Task<bool> ExecuteAsync(DownloadItem item, ILogger? logger = null)
        {
            try
            {
                logger?.LogInfo($"شروع انتقال فایل {item.FileName} به {DestinationPath}");

                var sourcePath = Path.Combine(item.SavePath, item.FileName);
                var destinationFile = Path.Combine(DestinationPath, item.FileName);

                if (!File.Exists(sourcePath))
                {
                    logger?.LogError($"فایل مبدأ وجود ندارد: {sourcePath}");
                    return false;
                }

                if (!Directory.Exists(DestinationPath))
                {
                    Directory.CreateDirectory(DestinationPath);
                }

                if (File.Exists(destinationFile) && !OverwriteExisting)
                {
                    logger?.LogWarning($"فایل مقصد وجود دارد و جایگزینی مجاز نیست: {destinationFile}");
                    return false;
                }

                File.Move(sourcePath, destinationFile, OverwriteExisting);
                
                // به‌روزرسانی مسیر در آیتم
                item.SavePath = DestinationPath;

                logger?.LogInfo($"فایل با موفقیت منتقل شد: {destinationFile}");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"خطا در انتقال فایل {item.FileName}", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// استخراج فایل فشرده
    /// </summary>
    public class ExtractArchiveAction : PostDownloadAction
    {
        public override string Name => "استخراج فایل فشرده";
        public override string Description => "استخراج فایل‌های فشرده ZIP، RAR، 7Z";

        /// <summary>
        /// مسیر استخراج
        /// </summary>
        public string? ExtractPath { get; set; }

        /// <summary>
        /// حذف فایل فشرده پس از استخراج
        /// </summary>
        public bool DeleteAfterExtraction { get; set; } = false;

        public ExtractArchiveAction(string? extractPath = null)
        {
            ExtractPath = extractPath;
        }

        public override async Task<bool> ExecuteAsync(DownloadItem item, ILogger? logger = null)
        {
            try
            {
                var filePath = Path.Combine(item.SavePath, item.FileName);
                var extension = Path.GetExtension(item.FileName).ToLower();

                if (!IsArchiveFile(extension))
                {
                    logger?.LogInfo($"فایل {item.FileName} فایل فشرده نیست، استخراج انجام نمی‌شود");
                    return true; // نه خطا، فقط قابل اجرا نیست
                }

                var extractTo = ExtractPath ?? Path.Combine(item.SavePath, Path.GetFileNameWithoutExtension(item.FileName));

                logger?.LogInfo($"شروع استخراج {item.FileName} به {extractTo}");

                if (!Directory.Exists(extractTo))
                {
                    Directory.CreateDirectory(extractTo);
                }

                bool success = extension switch
                {
                    ".zip" => await ExtractZipFile(filePath, extractTo, logger),
                    ".rar" => await ExtractRarFile(filePath, extractTo, logger),
                    ".7z" => await Extract7zFile(filePath, extractTo, logger),
                    _ => false
                };

                if (success && DeleteAfterExtraction)
                {
                    File.Delete(filePath);
                    logger?.LogInfo($"فایل فشرده حذف شد: {filePath}");
                }

                return success;
            }
            catch (Exception ex)
            {
                logger?.LogError($"خطا در استخراج فایل {item.FileName}", ex);
                return false;
            }
        }

        private bool IsArchiveFile(string extension)
        {
            return extension is ".zip" or ".rar" or ".7z";
        }

        private async Task<bool> ExtractZipFile(string filePath, string extractTo, ILogger? logger)
        {
            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(filePath, extractTo, true);
                logger?.LogInfo($"فایل ZIP با موفقیت استخراج شد: {extractTo}");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"خطا در استخراج فایل ZIP: {filePath}", ex);
                return false;
            }
        }

        private async Task<bool> ExtractRarFile(string filePath, string extractTo, ILogger? logger)
        {
            // برای RAR نیاز به ابزار خارجی داریم
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "unrar",
                        Arguments = $"x \"{filePath}\" \"{extractTo}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    logger?.LogInfo($"فایل RAR با موفقیت استخراج شد: {extractTo}");
                    return true;
                }
                else
                {
                    logger?.LogError($"خطا در استخراج فایل RAR - کد خروج: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"خطا در اجرای unrar برای {filePath}", ex);
                return false;
            }
        }

        private async Task<bool> Extract7zFile(string filePath, string extractTo, ILogger? logger)
        {
            // برای 7Z نیاز به ابزار خارجی داریم
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "7z",
                        Arguments = $"x \"{filePath}\" -o\"{extractTo}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    logger?.LogInfo($"فایل 7Z با موفقیت استخراج شد: {extractTo}");
                    return true;
                }
                else
                {
                    logger?.LogError($"خطا در استخراج فایل 7Z - کد خروج: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"خطا در اجرای 7z برای {filePath}", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// ارسال اطلاع‌رسانی
    /// </summary>
    public class NotificationAction : PostDownloadAction
    {
        public override string Name => "اطلاع‌رسانی";
        public override string Description => "ارسال اطلاع‌رسانی پس از تکمیل دانلود";

        /// <summary>
        /// نوع اطلاع‌رسانی
        /// </summary>
        public NotificationType Type { get; set; } = NotificationType.Toast;

        /// <summary>
        /// پیام سفارشی
        /// </summary>
        public string CustomMessage { get; set; }

        public override async Task<bool> ExecuteAsync(DownloadItem item, ILogger? logger = null)
        {
            try
            {
                var message = CustomMessage ?? $"دانلود فایل '{item.FileName}' تکمیل شد";

                switch (Type)
                {
                    case NotificationType.Toast:
                        await ShowToastNotification(message, logger);
                        break;
                    
                    case NotificationType.MessageBox:
                        await ShowMessageBox(message, logger);
                        break;
                    
                    case NotificationType.SystemTray:
                        await ShowSystemTrayNotification(message, logger);
                        break;
                }

                logger?.LogInfo($"اطلاع‌رسانی ارسال شد برای {item.FileName}");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"خطا در ارسال اطلاع‌رسانی برای {item.FileName}", ex);
                return false;
            }
        }

        private async Task ShowToastNotification(string message, ILogger? logger)
        {
            // پیاده‌سازی Toast Notification برای ویندوز
            try
            {
                // استفاده از Windows Toast Notification API
                logger?.LogDebug($"نمایش Toast: {message}");
            }
            catch (Exception ex)
            {
                logger?.LogError("خطا در نمایش Toast Notification", ex);
            }
        }

        private async Task ShowMessageBox(string message, ILogger? logger)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Windows.Dialog.CustomMessageBox.Show(message, "دانلود تکمیل شد", 
                        Windows.Dialog.CustomMessageBoxType.Success);
                });
            }
            catch (Exception ex)
            {
                logger?.LogError("خطا در نمایش MessageBox", ex);
            }
        }

        private async Task ShowSystemTrayNotification(string message, ILogger? logger)
        {
            try
            {
                // پیاده‌سازی System Tray Notification
                logger?.LogDebug($"نمایش System Tray: {message}");
            }
            catch (Exception ex)
            {
                logger?.LogError("خطا در نمایش System Tray Notification", ex);
            }
        }
    }

    /// <summary>
    /// انواع اطلاع‌رسانی
    /// </summary>
    public enum NotificationType
    {
        Toast,
        MessageBox,
        SystemTray
    }
}