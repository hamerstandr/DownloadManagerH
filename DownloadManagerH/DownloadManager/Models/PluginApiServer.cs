using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using DownloadManagerH.Models.Logging;
using DownloadManagerH.Windows;
using DownloadManagerH.Windows.Dialog;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Enhanced Plugin API Server for browser extension communication
    /// Provides secure, robust communication with browser extensions
    /// </summary>
    public class PluginApiServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly DownloadManager _downloadManager;
        private readonly ILogger _logger;
        private readonly int _port;
        private readonly string _ipAddress;
        private bool _isRunning = false;
        private readonly Dictionary<string, DateTime> _rateLimitTracker;
        private readonly object _rateLimitLock = new object();
        
        // Configuration
        private const int MAX_REQUESTS_PER_MINUTE = 60;
        private const int MAX_PAYLOAD_SIZE = 1024 * 1024; // 1MB
        private const string API_VERSION = "2.0";
        
        public event EventHandler<PluginEventArgs>? PluginEvent;
        
        public PluginApiServer(DownloadManager downloadManager, ILogger logger, int port = 24680, string ipAddress = "127.0.0.1")
        {
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _port = port;
            _ipAddress = ipAddress;
            _rateLimitTracker = [];
            
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{_ipAddress}:{_port}/");
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            
            _logger.LogInfo($"Plugin API Server initialized on {_ipAddress}:{_port}");
        }
        
        public async Task Start()
        {
            if (_isRunning) return;
            
            try
            {
                _listener.Start();
                _isRunning = true;
                
                _logger.LogInfo("Plugin API Server started successfully");
                
                // Start listening loop
                _ = Task.Run(ListenLoop);
                
                OnPluginEvent(new PluginEventArgs("ServerStarted", "Plugin API Server started", null));
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start Plugin API Server", ex);
                throw;
            }
        }
        
        public void Stop()
        {
            if (!_isRunning) return;
            
            try
            {
                _isRunning = false;
                _listener?.Stop();
                
                _logger.LogInfo("Plugin API Server stopped");
                OnPluginEvent(new PluginEventArgs("ServerStopped", "Plugin API Server stopped", null));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error stopping Plugin API Server", ex);
            }
        }
        
        private async Task ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    
                    // Handle request asynchronously
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException ex) when (!_isRunning)
                {
                    // Expected when stopping the server
                    _logger.LogDebug("HttpListener stopped", ex);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in listen loop", ex);
                    await Task.Delay(1000); // Brief delay before retrying
                }
            }
        }
        
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            try
            {
                // Set CORS headers
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, User-Agent");
                
                // Handle preflight requests
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }
                
                // Rate limiting
                if (!CheckRateLimit(request.RemoteEndPoint.Address.ToString()))
                {
                    await SendErrorResponse(response, 429, "Rate limit exceeded");
                    return;
                }
                
                // Validate request size
                if (request.ContentLength64 > MAX_PAYLOAD_SIZE)
                {
                    await SendErrorResponse(response, 413, "Payload too large");
                    return;
                }
                
                // Route request based on URL path
                var path = request.Url.AbsolutePath.ToLower();
                
                _logger.LogDebug($"Plugin API request: {request.HttpMethod} {path} from {request.RemoteEndPoint}");
                
                switch (path)
                {
                    case "/add/":
                        await HandleAddDownloadRequest(request, response);
                        break;
                        
                    case "/status/":
                        await HandleStatusRequest(request, response);
                        break;
                        
                    case "/settings/":
                        await HandleSettingsRequest(request, response);
                        break;
                        
                    case "/focus/":
                        await HandleFocusRequest(request, response);
                        break;
                        
                    case "/stats/":
                        await HandleStatsRequest(request, response);
                        break;
                        
                    default:
                        await SendErrorResponse(response, 404, "Endpoint not found");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling plugin request: {request?.Url}", ex);
                await SendErrorResponse(response, 500, "Internal server error");
            }
        }
        
        private async Task HandleAddDownloadRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.HttpMethod != "POST")
            {
                await SendErrorResponse(response, 405, "Method not allowed");
                return;
            }
            
            try
            {
                // Read request body
                string requestBody;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    await SendErrorResponse(response, 400, "Empty request body");
                    return;
                }
                
                // Parse request
                var downloadRequest = ParseDownloadRequest(requestBody);
                if (downloadRequest == null || !downloadRequest.Links.Any())
                {
                    await SendErrorResponse(response, 400, "No valid links found");
                    return;
                }
                
                // Validate links
                var validLinks = ValidateLinks(downloadRequest.Links);
                if (!validLinks.Any())
                {
                    await SendErrorResponse(response, 400, "No valid download links");
                    return;
                }
                
                // Process downloads
                var result = await ProcessDownloadRequest(downloadRequest, validLinks);
                
                // Send response
                await SendJsonResponse(response, 200, result);
                
                // Log successful request
                _logger.LogInfo($"Plugin download request processed: {validLinks.Count} links from {request.RemoteEndPoint}");
                
                OnPluginEvent(new PluginEventArgs("DownloadAdded", $"{validLinks.Count} downloads added", downloadRequest));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning($"Invalid JSON in plugin request: {ex.Message}");
                await SendErrorResponse(response, 400, "Invalid JSON format");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing download request", ex);
                await SendErrorResponse(response, 500, "Error processing request");
            }
        }
        
        private async Task HandleStatusRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            var status = new
            {
                status = "running",
                version = API_VERSION,
                timestamp = DateTime.UtcNow,
                activeDownloads = _downloadManager.GetActiveDownloadsCount(),
                totalDownloads = _downloadManager.GetTotalDownloadsCount(),
                serverInfo = new
                {
                    port = _port,
                    address = _ipAddress,
                    uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
                }
            };
            
            await SendJsonResponse(response, 200, status);
        }
        
        private async Task HandleSettingsRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.HttpMethod == "GET")
            {
                var settings = new
                {
                    defaultDownloadPath = Settings.DefaultDownloadPath,
                    addDownloadsDirectly = Settings.AddDownloadsDirectly,
                    maxConcurrentDownloads = Settings.MaxConcurrentDownloadsLimit,
                    enableClipboardMonitoring = Settings.MonitorClipboard
                };
                
                await SendJsonResponse(response, 200, settings);
            }
            else
            {
                await SendErrorResponse(response, 405, "Method not allowed");
            }
        }
        
        private async Task HandleFocusRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.HttpMethod != "POST")
            {
                await SendErrorResponse(response, 405, "Method not allowed");
                return;
            }
            
            try
            {
                // Focus main window on UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var mainWindow = MainWindow.Me;
                    if (mainWindow != null)
                    {
                        if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
                        {
                            mainWindow.WindowState = System.Windows.WindowState.Normal;
                        }
                        
                        mainWindow.Activate();
                        mainWindow.Focus();
                    }
                });
                
                await SendJsonResponse(response, 200, new { success = true, message = "Window focused" });
                
                _logger.LogInfo("Main window focused via plugin API");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error focusing main window", ex);
                await SendErrorResponse(response, 500, "Error focusing window");
            }
        }
        
        private async Task HandleStatsRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            var stats = new
            {
                downloads = new
                {
                    total = _downloadManager.GetTotalDownloadsCount(),
                    active = _downloadManager.GetActiveDownloadsCount(),
                    completed = _downloadManager.GetCompletedDownloadsCount(),
                    failed = _downloadManager.GetFailedDownloadsCount()
                },
                performance = new
                {
                    totalBytesDownloaded = _downloadManager.GetTotalBytesDownloaded(),
                    averageSpeed = _downloadManager.GetAverageDownloadSpeed(),
                    uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
                },
                system = new
                {
                    memoryUsage = GC.GetTotalMemory(false),
                    threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count
                }
            };
            
            await SendJsonResponse(response, 200, stats);
        }
        
        private PluginDownloadRequest? ParseDownloadRequest(string requestBody)
        {
            try
            {
                // Try to parse as enhanced request format first
                if (requestBody.TrimStart().StartsWith("{"))
                {
                    return JsonSerializer.Deserialize<PluginDownloadRequest>(requestBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                
                // Try to parse as simple array
                if (requestBody.TrimStart().StartsWith("["))
                {
                    var urls = JsonSerializer.Deserialize<string[]>(requestBody);
                    return new PluginDownloadRequest
                    {
                        Links = urls.Select(url => new PluginLinkData { Url = url }).ToList(),
                        Type = "simple",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }
                
                // Try to parse as single URL
                if (requestBody.StartsWith("http://") || requestBody.StartsWith("https://"))
                {
                    return new PluginDownloadRequest
                    {
                        Links = new List<PluginLinkData> { new PluginLinkData { Url = requestBody.Trim() } },
                        Type = "single",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error parsing download request: {ex.Message}");
                return null;
            }
        }
        
        private List<PluginLinkData> ValidateLinks(List<PluginLinkData> links)
        {
            var validLinks = new List<PluginLinkData>();
            
            foreach (var link in links)
            {
                if (string.IsNullOrWhiteSpace(link.Url))
                    continue;
                
                if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    validLinks.Add(link);
                }
                else
                {
                    _logger.LogWarning($"Invalid URL rejected: {link.Url}");
                }
            }
            
            return validLinks;
        }
        
        private async Task<PluginResponse> ProcessDownloadRequest(PluginDownloadRequest request, List<PluginLinkData> validLinks)
        {
            var addedDownloads = new List<string>();
            var errors = new List<string>();
            
            try
            {
                if (validLinks.Count == 1)
                {
                    // Single download
                    var link = validLinks[0];
                    var success = await ProcessSingleDownload(link);
                    
                    if (success)
                    {
                        addedDownloads.Add(link.Url);
                    }
                    else
                    {
                        errors.Add($"Failed to add: {link.Url}");
                    }
                }
                else
                {
                    // Batch download
                    await ProcessBatchDownload(validLinks, addedDownloads, errors);
                }
                
                return new PluginResponse
                {
                    Success = addedDownloads.Any(),
                    Message = $"Added {addedDownloads.Count} downloads",
                    AddedCount = addedDownloads.Count,
                    ErrorCount = errors.Count,
                    AddedUrls = addedDownloads,
                    Errors = errors
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing download request", ex);
                return new PluginResponse
                {
                    Success = false,
                    Message = "Internal error processing downloads",
                    ErrorCount = 1,
                    Errors = [ex.Message]
                };
            }
        }
        
        private async Task<bool> ProcessSingleDownload(PluginLinkData linkData)
        {
            try
            {
                if (Settings.AddDownloadsDirectly)
                {
                    // Add directly without dialog
                    var downloadItem = CreateDownloadItem(linkData);
                    _downloadManager.AddDownload(downloadItem);
                    _downloadManager.SaveDownloads();
                    
                    return true;
                }
                else
                {
                    // Show dialog on UI thread
                    var result = false;
                    
                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        try
                        {
                            var dialog = new InputDialog();
                            dialog.txtUrl.Text = linkData.Url;
                            dialog.txtFileName.Text = ExtractFileName(linkData.Url, linkData.Filename);
                            dialog.txtSavePath.Text = Settings.DefaultDownloadPath;
                            
                            if (dialog.ShowDialog() == true)
                            {
                                var downloadItem = new DownloadItem
                                {
                                    Url = dialog.Url,
                                    FileName = dialog.FileName,
                                    SavePath = dialog.SavePath,
                                    Group = dialog.Group,
                                    Status = DownloadStatus.Pending,
                                    Speed = "-",
                                    Headers = linkData.Headers,
                                    Referrer = linkData.Referrer,
                                    Cookies = linkData.Cookies,
                                    ParallelParts=Settings.ParallelParts
                                };
                                
                                _downloadManager.AddDownload(downloadItem);
                                _downloadManager.SaveDownloads();
                                MainWindow.Me?.RefreshList();
                                
                                result = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Error in single download dialog", ex);
                        }
                    });
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing single download: {linkData.Url}", ex);
                return false;
            }
        }
        
        private async Task ProcessBatchDownload(List<PluginLinkData> links, List<string> addedDownloads, List<string> errors)
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                try
                {
                    var groups = _downloadManager.GetGroups().ToList();
                    if (!groups.Contains("پیش‌فرض"))
                        groups.Insert(0, "پیش‌فرض");
                    
                    var linkItems = links.Select(l => new BulkLinkItem
                    {
                        Url = l.Url,
                        FileName = ExtractFileName(l.Url, l.Filename),
                        IsSelected = true
                    }).ToList();
                    
                    var dialog = new BulkAddDialog(linkItems.Select(l => l.Url).ToList(), groups, Settings.DefaultDownloadPath);
                    
                    if (dialog.ShowDialog() == true)
                    {
                        var selectedLinks = dialog.GetSelectedLinks();
                        
                        foreach (var selectedLink in selectedLinks)
                        {
                            try
                            {
                                var originalLink = links.FirstOrDefault(l => l.Url == selectedLink.Url);
                                if (originalLink==null) break;
                                var downloadItem = new DownloadItem
                                {
                                    Url = selectedLink.Url,
                                    FileName = ExtractFileName(selectedLink.Url),
                                    SavePath = dialog.SavePath,
                                    Group = dialog.SelectedGroup,
                                    Status = DownloadStatus.Pending,
                                    Speed = "-",
                                    Headers = originalLink.Headers,
                                    Referrer = originalLink.Referrer,
                                    Cookies = originalLink.Cookies,
                                    ParallelParts=Settings.ParallelParts
                                };
                                
                                _downloadManager.AddDownload(downloadItem);
                                addedDownloads.Add(selectedLink.Url);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error adding batch download: {selectedLink.Url}", ex);
                                errors.Add($"Failed to add: {selectedLink.Url}");
                            }
                        }
                        
                        if (addedDownloads.Any())
                        {
                            _downloadManager.SaveDownloads();
                            MainWindow.Me?.RefreshList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in batch download dialog", ex);
                    errors.Add("Error showing batch download dialog");
                }
            });
        }
        
        private static DownloadItem CreateDownloadItem(PluginLinkData linkData)
        {
            return new DownloadItem
            {
                Url = linkData.Url,
                FileName = ExtractFileName(linkData.Url, linkData.Filename),
                SavePath = Settings.DefaultDownloadPath,
                Status = DownloadStatus.Pending,
                Speed = "-",
                Headers = linkData.Headers,
                Referrer = linkData.Referrer,
                Cookies = linkData.Cookies,
                Metadata = new DownloadMetadata
                {
                    ContentType = linkData.Filetype ?? "application/octet-stream",
                    CreatedAt = linkData.Timestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(linkData.Timestamp).DateTime : DateTime.Now
                },
                ParallelParts=Settings.ParallelParts
            };
        }
        
        private static string ExtractFileName(string url, string suggestedName = null)
        {
            if (!string.IsNullOrWhiteSpace(suggestedName))
                return suggestedName;
            
            try
            {
                var uri = new Uri(url);
                var fileName = Path.GetFileName(uri.AbsolutePath);
                
                if (string.IsNullOrWhiteSpace(fileName) || fileName == "/")
                {
                    fileName = "download";
                }
                
                return fileName;
            }
            catch
            {
                return "download";
            }
        }
        
        private bool CheckRateLimit(string clientIp)
        {
            lock (_rateLimitLock)
            {
                var now = DateTime.UtcNow;
                var oneMinuteAgo = now.AddMinutes(-1);
                
                // Clean old entries
                var keysToRemove = _rateLimitTracker
                    .Where(kvp => kvp.Value < oneMinuteAgo)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in keysToRemove)
                {
                    _rateLimitTracker.Remove(key);
                }
                
                // Count requests from this IP in the last minute
                var requestCount = _rateLimitTracker.Count(kvp => kvp.Key.StartsWith(clientIp));
                
                if (requestCount >= MAX_REQUESTS_PER_MINUTE)
                {
                    _logger.LogWarning($"Rate limit exceeded for IP: {clientIp}");
                    return false;
                }
                
                // Add current request
                _rateLimitTracker[$"{clientIp}_{now.Ticks}"] = now;
                return true;
            }
        }
        
        private static async Task SendJsonResponse(HttpListenerResponse response, int statusCode, object data)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
            
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
        
        private async Task SendErrorResponse(HttpListenerResponse response, int statusCode, string message)
        {
            var error = new
            {
                error = true,
                statusCode = statusCode,
                message = message,
                timestamp = DateTime.UtcNow
            };
            
            await SendJsonResponse(response, statusCode, error);
        }
        
        protected virtual void OnPluginEvent(PluginEventArgs e)
        {
            PluginEvent?.Invoke(this, e);
        }
        
        public void Dispose()
        {
            Stop();
            _listener?.Close();
        }
    }
    
    // Supporting classes
    public class PluginDownloadRequest
    {
        public List<PluginLinkData> Links { get; set; } = new List<PluginLinkData>();
        public string Type { get; set; } = "";
        public long Timestamp { get; set; }
        public string ExtensionVersion { get; set; } = "";
    }
    
    public class PluginLinkData
    {
        public string Url { get; set; } = "";
        public string Filename { get; set; } = "";
        public string Filesize { get; set; } = "";  
        public string Filetype { get; set; } = "";
        public string Referrer { get; set; } = "";
        public string Title { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public long Timestamp { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];
        public List<string> Cookies { get; set; } = [];
    }
    
    public class PluginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int AddedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> AddedUrls { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }
    
    public class PluginEventArgs : EventArgs
    {
        public string EventType { get; }
        public string Message { get; }
        public object Data { get; }
        public DateTime Timestamp { get; }
        
        public PluginEventArgs(string eventType, string message, object data)
        {
            EventType = eventType;
            Message = message;
            Data = data;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    public class BulkLinkItem
    {
        public string Url { get; set; } = "";
        public string FileName { get; set; } = "";
        public bool IsSelected { get; set; }
    }
}