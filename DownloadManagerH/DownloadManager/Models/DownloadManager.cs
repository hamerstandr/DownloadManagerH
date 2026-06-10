using DownloadManagerH.Models.Logging;
using DownloadManagerH.Windows;
using DownloadManagerH.Windows.Dialog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace DownloadManagerH.Models
{
    public class DownloadManager : IDisposable
    {
        public List<DownloadItem> Downloads { get; set; } = [];
        private const string SaveFile = "downloads.json";
        private readonly SemaphoreSlim downloadSemaphore = new(Settings.CountConctionDownloads);
        private readonly System.Timers.Timer scheduleTimer;
        private readonly List<string> currentDownloadErrors = [];
        private readonly ILogger _logger;
        private readonly ConnectionPoolManager _connectionPool;
        private readonly ResourceManager _resourceManager;
        private readonly MemoryMonitor _memoryMonitor;
        private bool _disposed = false;
        
        public static string TempFilePath => Path.Combine(Settings.TempDirectory, "");
        public static string SaveFilePath => Path.Combine(Settings.DataDirectory, "downloads.json");
        public DownloadManager(ILogger logger = null)
        {
            _logger = logger ?? LoggerFactory.GetDefaultLogger();
            
            try
            {
                _logger.LogInfo("شروع مقداردهی اولیه مدیر دانلود");
                
                // مقداردهی اولیه مدیریت منابع
                _connectionPool = new ConnectionPoolManager(_logger);
                _resourceManager = new ResourceManager(_logger);
                _memoryMonitor = new MemoryMonitor(_logger);
                
                // ثبت مدیران منابع
                _resourceManager.RegisterResource("ConnectionPool", _connectionPool);
                _resourceManager.RegisterResource("MemoryMonitor", _memoryMonitor);
                
                // تنظیم event handler ها برای نظارت حافظه
                _memoryMonitor.MemoryPressureDetected += OnMemoryPressureDetected;
                _memoryMonitor.PotentialMemoryLeakDetected += OnPotentialMemoryLeakDetected;
                
                EnsureDataFolders();
                if (!Directory.Exists(Settings.TempDirectory))
                    Directory.CreateDirectory(Settings.TempDirectory);
                
                scheduleTimer = new System.Timers.Timer(10000); // هر ۱۰ ثانیه بررسی
                scheduleTimer.Elapsed += ScheduleTimer_Elapsed;
                scheduleTimer.Start();
                
                _logger.LogInfo("مقداردهی اولیه مدیر دانلود با موفقیت تکمیل شد");
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در مقداردهی اولیه مدیر دانلود", ex);
                throw;
            }
        }

        private static void EnsureDataFolders()
        {
            try
            {
                if (!Directory.Exists(Settings.DataDirectory))
                    Directory.CreateDirectory(Settings.DataDirectory);
                if (!Directory.Exists(Settings.TempDirectory))
                    Directory.CreateDirectory(Settings.TempDirectory);
                if (!Directory.Exists(Settings.DefaultDownloadPath))
                    Directory.CreateDirectory(Settings.DefaultDownloadPath);
            }
            catch { }
        }

        public static void SetDownloadPath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            Settings.DefaultDownloadPath = path;
        }

        private void ScheduleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            var scheduled = Downloads.Where(d => d.Status == DownloadStatus.Pending && d.ScheduledTime != null && d.ScheduledTime <= now).ToList();
            foreach (var item in scheduled)
            {
                _ = StartDownloadAsync(item);
            }
        }

        public event EventHandler<DownloadItem>? DownloadAdded;
        public event EventHandler<DownloadItem>? DownloadRemoved;
        public event EventHandler<DownloadItem>? DownloadUpdated;

        public void AddDownload(DownloadItem item)
        {
            try
            {
                Downloads.Add(item);
                _logger.LogDownloadEvent(item, "دانلود اضافه شد", new { TotalDownloads = Downloads.Count });
                DownloadAdded?.Invoke(this, item);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در افزودن دانلود: {item?.FileName}", ex, item);
                throw;
            }
        }

        public void RemoveDownload(DownloadItem item)
        {
            try
            {
                var removed = Downloads.Remove(item);
                if (removed)
                {
                    _logger.LogDownloadEvent(item, "دانلود حذف شد", new { TotalDownloads = Downloads.Count });
                    DownloadRemoved?.Invoke(this, item);
                }
                else
                {
                    _logger.LogWarning($"تلاش برای حذف دانلود موجود نبود: {item?.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطا در حذف دانلود: {item?.FileName}", ex, item);
                throw;
            }
        }

        public void UpdateDownload(DownloadItem item)
        {
            DownloadUpdated?.Invoke(this, item);
        }

        public void SaveDownloads()
        {
            try
            {
                _logger.LogDebug("شروع ذخیره لیست دانلودها");
                var json = JsonSerializer.Serialize(Downloads);
                File.WriteAllText(SaveFilePath, json);
                _logger.LogInfo($"لیست دانلودها با موفقیت ذخیره شد - تعداد: {Downloads.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در ذخیره لیست دانلودها", ex);
                throw;
            }
        }

        public void LoadDownloads()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    _logger.LogDebug("شروع بارگذاری لیست دانلودها");
                    var json = File.ReadAllText(SaveFilePath);
                    Downloads = JsonSerializer.Deserialize<List<DownloadItem>>(json) ?? new List<DownloadItem>();
                    _logger.LogInfo($"لیست دانلودها با موفقیت بارگذاری شد - تعداد: {Downloads.Count}");
                }
                else
                {
                    _logger.LogInfo("فایل لیست دانلودها وجود ندارد، لیست خالی ایجاد شد");
                    Downloads = new List<DownloadItem>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("خطا در بارگذاری لیست دانلودها", ex);
                Downloads = new List<DownloadItem>(); // لیست خالی در صورت خطا
            }
        }

        public IEnumerable<string> GetGroups()
        {
            return Downloads.Select(d => d.Group).Distinct().Where(g => !string.IsNullOrEmpty(g));
        }

        public int DownloadCount => Downloads?.Count ?? 0;
        public double AverageSpeed
        {
            get
            {
                var downloading = Downloads?.Where(d => d.Status == DownloadStatus.Downloading && !string.IsNullOrWhiteSpace(d.Speed));
                if (downloading == null || !downloading.Any())
                    return 0;
                var speeds = downloading.Select(d =>
                {
                    var s = d.Speed?.Replace("KB/s", "").Trim();
                    if (double.TryParse(s, out double val))
                        return val;
                    return 0d;
                });
                return speeds.Any() ? Math.Round(speeds.Average(), 1) : 0;
            }
        }



        // متدهای شروع، توقف، ادامه و مدیریت دانلودها با مدیریت خطا
        public async Task<bool> StartDownloadAsync(DownloadItem item)
        {
            await downloadSemaphore.WaitAsync();
            currentDownloadErrors.Clear(); // پاک کردن خطاهای قبلی
            
            _logger.LogDownloadEvent(item, "شروع دانلود", new { Url = item.Url, FileName = item.FileName });
            
            try
            {
                item.Status = DownloadStatus.Downloading;
                EnsureDataFolders();
                // مسیر موقت برای دانلود
                string tempFile = Path.Combine(Settings.TempDirectory, item.FileName);
                string finalFile = Path.Combine(item.SavePath, item.FileName);
                
                // محاسبه مقدار دانلود شده از بخش‌های موجود (برای ادامه دانلود)
                if (item.Parts.Count > 0 && item.DownloadedBytes == 0)
                {
                    // جمع‌آوری مقدار دانلود شده از تمام بخش‌ها
                    long totalDownloaded = item.Parts.Sum(p => p.Downloaded);
                    item.DownloadedBytes = totalDownloaded;
                    
                    if (totalDownloaded > 0)
                    {
                        _logger.LogInfo($"ادامه دانلود - مقدار دانلود شده: {totalDownloaded} بایت", new { FileName = item.FileName });
                    }
                }
                
                // اگر بخش‌ها از قبل وجود دارند (ادامه دانلود)، مستقیماً به دانلود بخش‌ها برو
                if (item.Parts.Count == 0)
                {
                    // بررسی و اصلاح URL
                    if (!Uri.TryCreate(item.Url, UriKind.Absolute, out Uri? uri))
                    {
                        item.Status = DownloadStatus.Failed;
                        var errorMsg = $"آدرس URL نامعتبر است: {item.Url}";
                        currentDownloadErrors.Add(errorMsg);
                        _logger.LogError(errorMsg, null, item);
                        _logger.LogDownloadEvent(item, "دانلود ناموفق - URL نامعتبر");
                        ShowDownloadErrorSummary(item);
                        return false;
                    }
                    
                    PooledHttpClient? pooledClient = null;
                    try
                    {
                        // دریافت HttpClient از connection pool
                        var host = uri.Host;
                        pooledClient = await _connectionPool.GetHttpClientAsync(host);
                        var client = pooledClient.Client;
                        
                        var request = new HttpRequestMessage(HttpMethod.Head, uri);
                        var response = await client.SendAsync(request);
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            item.Status = DownloadStatus.Failed;
                            var errorMsg = $"خطا در دریافت اطلاعات فایل. کد خطا: {response.StatusCode}";
                            currentDownloadErrors.Add(errorMsg);
                            _logger.LogError(errorMsg, null, new { Url = item.Url, StatusCode = response.StatusCode });
                            _logger.LogDownloadEvent(item, "دانلود ناموفق - خطای HTTP");
                            ShowDownloadErrorSummary(item);
                            pooledClient.ReportError();
                            return false;
                        }
                        
                        item.TotalBytes = response.Content.Headers.ContentLength ?? 0;
                        
                        if (item.TotalBytes == 0)
                        {
                            item.Status = DownloadStatus.Failed;
                            var errorMsg = "حجم فایل نامشخص است یا فایل خالی است.";
                            currentDownloadErrors.Add(errorMsg);
                            _logger.LogError(errorMsg, null, item);
                            _logger.LogDownloadEvent(item, "دانلود ناموفق - حجم فایل صفر");
                            ShowDownloadErrorSummary(item);
                            return false;
                        }
                        
                        _logger.LogInfo($"اطلاعات فایل دریافت شد - حجم: {item.TotalBytes} بایت", new { FileName = item.FileName, Size = item.TotalBytes });
                        
                        long partSize = item.TotalBytes / item.ParallelParts;
                        for (int i = 0; i < item.ParallelParts; i++)
                        {
                            long start = i * partSize;
                            long end = i == item.ParallelParts - 1 ? item.TotalBytes - 1 : start + partSize - 1;
                            item.Parts.Add(new DownloadPart { Index = i, Start = start, End = end, Status = PartStatus.Pending });
                        }
                        
                        _logger.LogDebug($"فایل به {item.ParallelParts} بخش تقسیم شد", new { item.FileName, Parts = item.ParallelParts });
                        if (item.DownloadedBytes > 0)
                            item.Progress = ((double)item.DownloadedBytes / item.TotalBytes) * 100;
                        else
                            item.Progress = 0;
                    }
                    catch (HttpRequestException ex)
                    {
                        item.Status = DownloadStatus.Failed;
                        var errorMsg = $"خطا در اتصال به سرور: {ex.Message}";
                        currentDownloadErrors.Add(errorMsg);
                        _logger.LogError(errorMsg, ex, item);
                        _logger.LogDownloadEvent(item, "دانلود ناموفق - خطای شبکه");
                        ShowDownloadErrorSummary(item);
                        pooledClient?.ReportError();
                        return false;
                    }
                    catch (TaskCanceledException ex)
                    {
                        item.Status = DownloadStatus.Failed;
                        var errorMsg = "درخواست به سرور تایم‌اوت شد.";
                        currentDownloadErrors.Add(errorMsg);
                        _logger.LogError(errorMsg, ex, item);
                        _logger.LogDownloadEvent(item, "دانلود ناموفق - تایم‌اوت");
                        ShowDownloadErrorSummary(item);
                        pooledClient?.ReportError();
                        return false;
                    }
                    catch (Exception ex)
                    {
                        item.Status = DownloadStatus.Failed;
                        var errorMsg = $"خطای غیرمنتظره: {ex.Message}";
                        currentDownloadErrors.Add(errorMsg);
                        _logger.LogError(errorMsg, ex, item);
                        _logger.LogDownloadEvent(item, "دانلود ناموفق - خطای غیرمنتظره");
                        ShowDownloadErrorSummary(item);
                        pooledClient?.ReportError();
                        return false;
                    }
                    finally
                    {
                        // بازگرداندن HttpClient به pool
                        if (pooledClient != null)
                        {
                            _connectionPool.ReturnHttpClient(pooledClient);
                        }
                    }
                }
                
                // شروع دانلود بخش‌ها (در temp)
                foreach (var part in item.Parts)
                {
                    part.TempFilePath = tempFile;
                    // اگر بخش قبلاً کامل شده، آن را رد کن
                    if (part.Status == PartStatus.Completed)
                        continue;
                    // برای ادامه دانلود، بخش‌های متوقف شده را به وضعیت Pending برگردان
                    if (part.Status == PartStatus.Failed || part.Status == PartStatus.Downloading)
                        part.Status = PartStatus.Pending;
                }
                var tasks = item.Parts.Where(p => p.Status != PartStatus.Completed).Select(part => DownloadPartAsync(item, part, tempFile));
                await Task.WhenAll(tasks);
                
                // بررسی وضعیت توقف یا暂停 - اگر دانلود متوقف شده، از تکمیل شدن جلوگیری کن
                if (item.Status == DownloadStatus.Paused || item.Status == DownloadStatus.Stopped)
                {
                    return false;
                }
                
                // بررسی نتیجه دانلود
                if (item.Parts.All(p => p.Status == PartStatus.Completed))
                {
                    item.Status = DownloadStatus.Completed;
                    // انتقال فایل از temp به مقصد
                    try
                    {
                        if (!Directory.Exists(item.SavePath))
                            Directory.CreateDirectory(item.SavePath);
                        File.Move(tempFile, finalFile, true);
                        
                        _logger.LogDownloadEvent(item, "دانلود تکمیل شد", new { 
                            FinalPath = finalFile, 
                            Size = item.TotalBytes,
                            Parts = item.Parts.Count 
                        });
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"انتقال فایل به مقصد با خطا مواجه شد: {ex.Message}";
                        currentDownloadErrors.Add(errorMsg);
                        _logger.LogError(errorMsg, ex, new { TempFile = tempFile, FinalFile = finalFile });
                        ShowDownloadErrorSummary(item);
                        return false;
                    }
                    //todo show windows complet
                    ShowCompeleteDalog(item);
                    //ShowSuccessMessage($"دانلود فایل '{item.FileName}' با موفقیت تکمیل شد.", "دانلود موفق");
                    return true;
                }
                else
                {
                    // اگر دانلود متوقف شده، وضعیت را تغییر نده
                    if (item.Status != DownloadStatus.Paused && item.Status != DownloadStatus.Stopped)
                    {
                        item.Status = DownloadStatus.Failed;
                    }
                    var failedParts = item.Parts.Count(p => p.Status == PartStatus.Failed);
                    var errorMsg = $"دانلود فایل '{item.FileName}' ناموفق بود. {failedParts} بخش از {item.Parts.Count} بخش با خطا مواجه شد.";
                    currentDownloadErrors.Add(errorMsg);
                    _logger.LogError(errorMsg, null, new { 
                        FileName = item.FileName, 
                        FailedParts = failedParts, 
                        TotalParts = item.Parts.Count 
                    });
                    _logger.LogDownloadEvent(item, "دانلود ناموفق - بخش‌های ناقص");
                    ShowDownloadErrorSummary(item);
                    return false;
                }
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                var errorMsg = $"خطای غیرمنتظره در دانلود: {ex.Message}";
                currentDownloadErrors.Add(errorMsg);
                _logger.LogError(errorMsg, ex, item);
                _logger.LogDownloadEvent(item, "دانلود ناموفق - خطای غیرمنتظره");
                ShowDownloadErrorSummary(item);
                return false;
            }
            finally
            {
                downloadSemaphore.Release();
                SaveDownloads();
            }
        }

        private async Task DownloadPartAsync(DownloadItem item, DownloadPart part, string tempFile)
        {
            if (part.Status == PartStatus.Completed) return;
            part.Status = PartStatus.Downloading;
            
            PooledHttpClient pooledClient = null;
            try
            {
                if (!Uri.TryCreate(item.Url, UriKind.Absolute, out Uri uri))
                {
                    part.Status = PartStatus.Failed;
                    return;
                }

                // دریافت HttpClient از connection pool
                var host = uri.Host;
                pooledClient = await _connectionPool.GetHttpClientAsync(host);
                var client = pooledClient.Client;
                
                // ثبت فایل موقت برای پاک‌سازی
                _resourceManager.RegisterTempFile(tempFile);
                
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(part.Start + part.Downloaded, part.End);
                
                var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (!res.IsSuccessStatusCode)
                {
                    part.Status = PartStatus.Failed;
                    pooledClient.ReportError();
                    return;
                }
                if (res.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    part.Status = PartStatus.Failed;
                    return;
                }
                
                var directory = Path.GetDirectoryName(tempFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                
                // استفاده از buffer pool برای بهتر بودن عملکرد
                using var fs = new FileStream(tempFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
                fs.Seek(part.Start + part.Downloaded, SeekOrigin.Begin);
                using var stream = await res.Content.ReadAsStreamAsync();
                
                // استفاده از performance manager برای tracking
                using var performanceTracker = _resourceManager.GetResource<PerformanceManager>("PerformanceManager")?.StartTracking($"DownloadPart_{part.Index}");
                
                var buffer = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, read);
                    part.Downloaded += read;
                    item.DownloadedBytes += read;
                    item.Speed = $"{read / 1024} KB/s";
                    //add clander per
                    item.Progress = ((double)item.DownloadedBytes / item.TotalBytes) * 100;

                    if (part.Downloaded % 102400 == 0)
                        SaveDownloads();
                    if (item.Status==DownloadStatus.Paused)
                    {
                        part.Status = PartStatus.Pending; // حفظ وضعیت برای ادامه بعدی
                        break;
                    }
                    else if (item.Status==DownloadStatus.Stopped)
                    {
                        part.Status = PartStatus.Failed; // توقف کامل
                        return;
                    }
                }
                
                // فقط در صورتی وضعیت را Completed قرار بده که متوقف نشده باشد
                if (item.Status != DownloadStatus.Paused && item.Status != DownloadStatus.Stopped)
                {
                    part.Status = PartStatus.Completed;
                }
            }
            catch (IOException ex)
            {
                part.Status = PartStatus.Failed;
                currentDownloadErrors.Add($"خطا در نوشتن فایل (بخش {part.Index}): {ex.Message}");
                pooledClient?.ReportError();
            }
            catch (Exception ex)
            {
                part.Status = PartStatus.Failed;
                currentDownloadErrors.Add($"خطای غیرمنتظره در دانلود بخش {part.Index}: {ex.Message}");
                pooledClient?.ReportError();
            }
            finally
            {
                // بازگرداندن HttpClient به pool
                if (pooledClient != null)
                {
                    _connectionPool.ReturnHttpClient(pooledClient);
                }
            }
        }

        public void PauseDownload(DownloadItem item)
        {
            if (item.CanResume)
                item.Status = DownloadStatus.Paused;
            else
                item.Status = DownloadStatus.Stopped;
            
            // ذخیره فوری وضعیت دانلود
            SaveDownloads();
        }

        public static async Task ResumeDownload(DownloadItem item)
        {
            var manager = MainWindow.Me?.manager;
            if (manager == null) return;
            
            // تغییر وضعیت به در حال دانلود
            item.Status = DownloadStatus.Downloading;
            
            // شروع مجدد دانلود
            await manager.StartDownloadAsync(item);
        }
        
        // متدهای نمایش پیام
        private static void ShowErrorMessage(string message, string title)
        {
            try
            {
                // استفاده از Dispatcher برای نمایش پیام در UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CustomMessageBox.Show(message, title, CustomMessageBoxType.Error);
                });
            }
            catch
            {
                // در صورت عدم دسترسی به UI، پیام را در کنسول نمایش دهید
                Console.WriteLine($"خطا: {title} - {message}");
            }
        }
        
        private static void ShowSuccessMessage(string message, string title)
        {
            try
            {
                // استفاده از Dispatcher برای نمایش پیام در UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CustomMessageBox.Show(message, title, CustomMessageBoxType.Success);
                });
            }
            catch
            {
                // در صورت عدم دسترسی به UI، پیام را در کنسول نمایش دهید
                Console.WriteLine($"موفقیت: {title} - {message}");
            }
        }
        private static void ShowCompeleteDalog(DownloadItem item)
        {
            try
            {
                // استفاده از Dispatcher برای نمایش پیام در UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CompletDialog.Show(item);
                });
            }
            catch
            {
                // در صورت عدم دسترسی به UI، پیام را در کنسول نمایش دهید
                ShowSuccessMessage($"دانلود فایل '{item.FileName}' با موفقیت تکمیل شد.", "دانلود موفق");
            }
        }
        private static void ShowWarningMessage(string message, string title)
        {
            try
            {
                // استفاده از Dispatcher برای نمایش پیام در UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CustomMessageBox.Show(message, title, CustomMessageBoxType.Warning);
                });
            }
            catch
            {
                // در صورت عدم دسترسی به UI، پیام را در کنسول نمایش دهید
                Console.WriteLine($"هشدار: {title} - {message}");
            }
        }
        
        private void ShowDownloadErrorSummary(DownloadItem item)
        {
            if (currentDownloadErrors.Count == 0) return;
            
            try
            {
                // استفاده از Dispatcher برای نمایش پیام در UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var mainMessage = $"خطا در دانلود فایل '{item.FileName}'";
                    var details = string.Join("\n", currentDownloadErrors);
                    
                    CustomMessageBox.Show(mainMessage, "خطای دانلود", CustomMessageBoxType.Error, details);
                });
            }
            catch
            {
                // در صورت عدم دسترسی به UI، پیام را در کنسول نمایش دهید
                Console.WriteLine($"خطای دانلود: {item.FileName}");
                foreach (var error in currentDownloadErrors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        }
        
        // متدهای آماری برای Plugin API Server
        public int GetActiveDownloadsCount()
        {
            return Downloads?.Count(d => d.Status == DownloadStatus.Downloading || d.Status == DownloadStatus.Pending) ?? 0;
        }
        
        public int GetTotalDownloadsCount()
        {
            return Downloads?.Count ?? 0;
        }
        
        public int GetCompletedDownloadsCount()
        {
            return Downloads?.Count(d => d.Status == DownloadStatus.Completed) ?? 0;
        }
        
        public int GetFailedDownloadsCount()
        {
            return Downloads?.Count(d => d.Status == DownloadStatus.Failed || d.Status == DownloadStatus.Stopped) ?? 0;
        }
        
        public long GetTotalBytesDownloaded()
        {
            return Downloads?.Where(d => d.Status == DownloadStatus.Completed)
                           .Sum(d => d.TotalBytes) ?? 0;
        }
        
        public double GetAverageDownloadSpeed()
        {
            var activeDownloads = Downloads?.Where(d => d.Status == DownloadStatus.Downloading).ToList();
            if (activeDownloads == null || !activeDownloads.Any())
                return 0;
            
            var speeds = activeDownloads
                .Where(d => !string.IsNullOrEmpty(d.Speed) && d.Speed != "-")
                .Select(d =>
                {
                    var speedText = d.Speed.Replace(" KB/s", "").Replace(" MB/s", "").Replace(" GB/s", "");
                    if (double.TryParse(speedText, out var speed))
                    {
                        if (d.Speed.Contains("MB/s"))
                            return speed * 1024; // Convert to KB/s
                        if (d.Speed.Contains("GB/s"))
                            return speed * 1024 * 1024; // Convert to KB/s
                        return speed;
                    }
                    return 0.0;
                });
            
            return speeds.Any() ? Math.Round(speeds.Average(), 1) : 0;
        }

        /// <summary>
        /// مدیریت فشار حافظه
        /// </summary>
        private async void OnMemoryPressureDetected(object sender, MemoryPressureEventArgs e)
        {
            _logger.LogWarning($"فشار حافظه تشخیص داده شد: {e.Level} - {e.CurrentMemoryUsage / (1024 * 1024)} MB");
            
            if (e.Level == MemoryPressureLevel.Critical)
            {
                // توقف دانلودهای جدید تا کاهش فشار حافظه
                _logger.LogWarning("به دلیل فشار حافظه بحرانی، دانلودهای جدید موقتاً متوقف می‌شوند");
                
                // اجرای پاک‌سازی فوری
                await _resourceManager.ForceCleanupAsync();
                await _memoryMonitor.PerformCleanupAsync();
            }
        }

        /// <summary>
        /// مدیریت نشت حافظه احتمالی
        /// </summary>
        private void OnPotentialMemoryLeakDetected(object sender, MemoryLeakEventArgs e)
        {
            _logger.LogError($"نشت حافظه احتمالی: {e.MemoryIncreaseRate / (1024 * 1024):F2} MB/min در {e.TimeSpan.TotalMinutes:F1} دقیقه");
            
            // اجرای پاک‌سازی اضطراری
            Task.Run(async () =>
            {
                await _resourceManager.ForceCleanupAsync();
                await _memoryMonitor.PerformCleanupAsync();
            });
        }

        /// <summary>
        /// دریافت آمار مدیریت منابع
        /// </summary>
        public ResourceManagementStatistics GetResourceStatistics()
        {
            return new ResourceManagementStatistics
            {
                ConnectionPoolStats = _connectionPool?.GetStatistics(),
                ResourceStats = _resourceManager?.GetStatistics(),
                MemoryStats = _memoryMonitor?.GetStatistics()
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                try
                {
                    _logger.LogInfo("شروع dispose کردن DownloadManager");
                    
                    // توقف تایمر
                    scheduleTimer?.Stop();
                    scheduleTimer?.Dispose();
                    
                    // dispose کردن semaphore
                    downloadSemaphore?.Dispose();
                    
                    // dispose کردن مدیران منابع
                    _connectionPool?.Dispose();
                    _resourceManager?.Dispose();
                    _memoryMonitor?.Dispose();
                    
                    _logger.LogInfo("DownloadManager با موفقیت dispose شد");
                }
                catch (Exception ex)
                {
                    _logger.LogError("خطا در dispose کردن DownloadManager", ex);
                }
            }
        }
    }

    /// <summary>
    /// آمار مدیریت منابع
    /// </summary>
    public class ResourceManagementStatistics
    {
        public ConnectionPoolStatistics ConnectionPoolStats { get; set; }
        public ResourceStatistics ResourceStats { get; set; }
        public MemoryStatistics MemoryStats { get; set; }
    }
} 