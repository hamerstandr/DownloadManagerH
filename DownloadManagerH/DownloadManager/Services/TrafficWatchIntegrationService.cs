using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Services
{
    /// <summary>
    /// سرویس یکپارچه‌سازی با TrafficWatch
    /// ارائه API برای نمایش اطلاعات دانلود منیجر در داشبورد وب TrafficWatch
    /// </summary>
    public class TrafficWatchIntegrationService : IDisposable
    {
        private WebApplication? _app;
        private bool _isEnabled = false;
        private int _port = 9090;
        private readonly ILogger _logger;
        private readonly DownloadManager _downloadManager;
        private bool _disposed = false;

        public TrafficWatchIntegrationService(DownloadManager downloadManager, ILogger logger)
        {
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Initialize(bool enabled, int port = 9090)
        {
            _isEnabled = enabled;
            _port = port;

            if (!_isEnabled)
            {
                _logger.LogInfo("TrafficWatch integration is disabled");
                return;
            }

            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls($"http://127.0.0.1:{_port}");
                
                _app = builder.Build();

                // Endpoint وضعیت کلی
                _app.MapGet("/api/status", async context =>
                {
                    try
                    {
                        var status = GetDownloadManagerStatus();
                        await RespondWithJson(context, status);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error in /api/status endpoint", ex);
                        await RespondWithError(context, 500, "Internal server error");
                    }
                });

                // Endpoint دانلودهای فعال
                _app.MapGet("/api/downloads/active", async context =>
                {
                    try
                    {
                        var downloads = GetActiveDownloads();
                        await RespondWithJson(context, downloads);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error in /api/downloads/active endpoint", ex);
                        await RespondWithError(context, 500, "Internal server error");
                    }
                });

                // Endpoint آمار تاریخی (اختیاری)
                _app.MapGet("/api/stats/history", async context =>
                {
                    try
                    {
                        var days = context.Request.Query["days"].FirstOrDefault();
                        var stats = GetHistoryStats(int.TryParse(days, out var d) ? d : 7);
                        await RespondWithJson(context, stats);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error in /api/stats/history endpoint", ex);
                        await RespondWithError(context, 500, "Internal server error");
                    }
                });

                // Endpoint تنظیمات (اختیاری)
                _app.MapGet("/api/settings", async context =>
                {
                    try
                    {
                        var settings = GetCurrentSettings();
                        await RespondWithJson(context, settings);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error in /api/settings endpoint", ex);
                        await RespondWithError(context, 500, "Internal server error");
                    }
                });

                // شروع سرور در thread جداگانه
                _ = Task.Run(() => _app.Run());
                
                _logger.LogInfo($"TrafficWatch API started on port {_port}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start TrafficWatch API", ex);
                _isEnabled = false;
            }
        }

        private async Task RespondWithJson(HttpContext context, object data)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(data, options);
            await context.Response.WriteAsync(json);
        }

        private async Task RespondWithError(HttpContext context, int statusCode, string message)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = statusCode;
            var error = new { error = true, statusCode, message };
            var json = JsonSerializer.Serialize(error);
            await context.Response.WriteAsync(json);
        }

        private object GetDownloadManagerStatus()
        {
            try
            {
                var activeCount = _downloadManager.GetActiveDownloadsCount();
                var completedCount = _downloadManager.GetCompletedDownloadsCount();
                var totalSpeed = _downloadManager.GetAverageDownloadSpeed() * 1024; // Convert KB/s to bytes/s
                
                return new
                {
                    isRunning = true,
                    version = "2.0.0",
                    activeDownloads = activeCount,
                    queuedDownloads = _downloadManager.Downloads.Count(d => d.Status == DownloadStatus.Pending),
                    completedDownloads = completedCount,
                    totalDownloadSpeed = totalSpeed > 0 ? (long)totalSpeed : 0,
                    totalUploadedSpeed = 0L, // Upload not implemented yet
                    downloadLimit = 0L, // No limit set
                    uploadLimit = 0L,
                    schedulerEnabled = true,
                    clipboardMonitorEnabled = Settings.MonitorClipboard,
                    browserIntegrationEnabled = true,
                    lastError = "",
                    apiEndpoint = $"http://127.0.0.1:{_port}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting download manager status", ex);
                return new
                {
                    isRunning = false,
                    version = "2.0.0",
                    activeDownloads = 0,
                    queuedDownloads = 0,
                    completedDownloads = 0,
                    totalDownloadSpeed = 0L,
                    totalUploadedSpeed = 0L,
                    downloadLimit = 0L,
                    uploadLimit = 0L,
                    schedulerEnabled = false,
                    clipboardMonitorEnabled = false,
                    browserIntegrationEnabled = false,
                    lastError = ex.Message,
                    apiEndpoint = $"http://127.0.0.1:{_port}"
                };
            }
        }

        private object GetActiveDownloads()
        {
            try
            {
                var downloads = _downloadManager.Downloads
                    .Where(d => d.Status == DownloadStatus.Downloading || 
                               d.Status == DownloadStatus.Paused || 
                               d.Status == DownloadStatus.Pending)
                    .Select(d => new
                    {
                        id = Guid.NewGuid().ToString(), // In real implementation, use actual ID
                        fileName = d.FileName,
                        url = d.Url,
                        status = d.Status.ToString(),
                        progress = Math.Round(d.Progress, 1),
                        downloadedSize = d.DownloadedBytes,
                        totalSize = d.TotalBytes,
                        speed = ParseSpeedToBytes(d.Speed),
                        eta = CalculateETA(d),
                        category = GetCategoryFromFileName(d.FileName)
                    }).ToList();
                
                return downloads;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting active downloads", ex);
                return Array.Empty<object>();
            }
        }

        private object GetHistoryStats(int days)
        {
            // Simple implementation - can be enhanced with actual historical data
            var dailyStats = Enumerable.Range(0, days)
                .Select(i =>
                {
                    var date = DateTime.Now.AddDays(-i).Date;
                    return new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        totalDownloaded = 0L,
                        totalUploaded = 0L,
                        downloadCount = 0,
                        averageSpeed = 0L
                    };
                }).Reverse().ToList();

            return new
            {
                dailyStats = dailyStats,
                weeklyTotal = 0L,
                monthlyTotal = 0L
            };
        }

        private object GetCurrentSettings()
        {
            return new
            {
                maxConcurrentDownloads = Settings.MaxConcurrentDownloadsLimit,
                defaultSavePath = Settings.DefaultDownloadPath,
                autoStart = Settings.EnableStartup,
                theme = "Dark",
                language = "fa",
                notificationsEnabled = true
            };
        }

        private static long ParseSpeedToBytes(string speed)
        {
            if (string.IsNullOrEmpty(speed) || speed == "-")
                return 0;

            try
            {
                speed = speed.Trim();
                if (speed.Contains("MB/s"))
                {
                    var value = double.Parse(speed.Replace("MB/s", "").Trim());
                    return (long)(value * 1024 * 1024);
                }
                if (speed.Contains("KB/s"))
                {
                    var value = double.Parse(speed.Replace("KB/s", "").Trim());
                    return (long)(value * 1024);
                }
                if (speed.Contains("GB/s"))
                {
                    var value = double.Parse(speed.Replace("GB/s", "").Trim());
                    return (long)(value * 1024 * 1024 * 1024);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string CalculateETA(DownloadItem item)
        {
            if (item.Status != DownloadStatus.Downloading || item.TotalBytes <= 0)
                return "00:00:00";

            var remainingBytes = item.TotalBytes - item.DownloadedBytes;
            var speed = ParseSpeedToBytes(item.Speed);

            if (speed <= 0)
                return "--:--:--";

            var seconds = remainingBytes / speed;
            var timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        private static string GetCategoryFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Other";

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".iso" or ".img" => "ISO",
                ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" => "Video",
                ".mp3" or ".wav" or ".flac" or ".aac" => "Audio",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archive",
                ".exe" or ".msi" or ".deb" or ".rpm" => "Application",
                ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" => "Document",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "Image",
                _ => "Other"
            };
        }

        public void Stop()
        {
            if (_app != null)
            {
                try
                {
                    _app.StopAsync().Wait(TimeSpan.FromSeconds(5));
                    _logger.LogInfo("TrafficWatch API stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error stopping TrafficWatch API", ex);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Stop();
                _app?.Dispose();
            }
        }
    }
}
