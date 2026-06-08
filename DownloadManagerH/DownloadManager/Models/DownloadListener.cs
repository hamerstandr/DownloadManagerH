using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DownloadManagerH.Models;
using DownloadManagerH.Models.Logging;
using DownloadManagerH.Windows.Dialog;
using System.Text.RegularExpressions;
using System.Linq;
using DownloadManagerH.Windows;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Enhanced Download Listener with improved plugin communication
    /// This class now serves as a wrapper around the new PluginApiServer
    /// </summary>
    public class DownloadListener : IDisposable
    {
        private readonly PluginApiServer _pluginApiServer;
        private readonly DownloadManager _manager;
        private readonly ILogger _logger;
        private readonly int _port;
        private readonly string _ip;
        private bool _isRunning = false;

        public DownloadListener(DownloadManager manager, int port = 24680, string ip = "127.0.0.1")
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _port = port;
            _ip = ip;
            
            // Get logger from manager or create default
            _logger = LoggerFactory.GetDefaultLogger();
            
            // Create the enhanced plugin API server
            _pluginApiServer = new PluginApiServer(_manager, _logger, _port, _ip);
            
            // Subscribe to plugin events
            _pluginApiServer.PluginEvent += OnPluginEvent;
            
            _logger.LogInfo($"Enhanced Download Listener initialized on {_ip}:{_port}");
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;
            
            try
            {
                await _pluginApiServer.Start();
                _isRunning = true;
                _logger.LogInfo("Enhanced Download Listener started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start Enhanced Download Listener", ex);
                throw;
            }
        }
        
        public void Start()
        {
            // Synchronous wrapper for backward compatibility
            try
            {
                StartAsync().Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error starting Download Listener", ex);
                throw;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            
            try
            {
                _pluginApiServer.Stop();
                _isRunning = false;
                _logger.LogInfo("Enhanced Download Listener stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error stopping Download Listener", ex);
            }
        }

        private void OnPluginEvent(object sender, PluginEventArgs e)
        {
            try
            {
                _logger.LogInfo($"Plugin Event: {e.EventType} - {e.Message}");
                
                // Handle specific plugin events
                switch (e.EventType)
                {
                    case "DownloadAdded":
                        ShowNotification("دانلود منجر حامد", e.Message, false);
                        break;
                        
                    case "ServerStarted":
                        ShowNotification("سرور افزونه", "سرور افزونه با موفقیت راه‌اندازی شد", false);
                        break;
                        
                    case "ServerStopped":
                        ShowNotification("سرور افزونه", "سرور افزونه متوقف شد", false);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling plugin event", ex);
            }
        }
        
        // Backward compatibility methods - these now delegate to the PluginApiServer
        [Obsolete("Use the enhanced PluginApiServer methods instead")]
        private System.Collections.Generic.List<string> ParseLinksFromBody(string body)
        {
            var links = new System.Collections.Generic.List<string>();
            try
            {
                // اگر JSON array
                if (body.TrimStart().StartsWith("["))
                {
                    var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(body);
                    if (arr != null) links.AddRange(arr);
                }
                // اگر JSON object با فیلد links
                else if (body.TrimStart().StartsWith("{"))
                {
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("links", out var linksProp) && linksProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var l in linksProp.EnumerateArray())
                            if (l.ValueKind == System.Text.Json.JsonValueKind.String)
                                links.Add(l.GetString()+"");
                    }
                    else if (doc.RootElement.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        links.Add(urlProp.GetString() + "");
                    }
                }
                // اگر HTML
                else if (body.Contains("<a ") || body.Contains("<A "))
                {
                    var matches = Regex.Matches(body, "href=[\"']([^\"'>]+)", RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        var url = m.Groups[1].Value;
                        if (url.StartsWith("http://") || url.StartsWith("https://"))
                            links.Add(url);
                    }
                }
                // اگر متن چند خطی
                else if (body.Contains("\n") || body.Contains("\r"))
                {
                    var lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                        if (line.StartsWith("http://") || line.StartsWith("https://"))
                            links.Add(line.Trim());
                }
                // اگر فقط یک لینک تکی
                else if (body.StartsWith("http://") || body.StartsWith("https://"))
                {
                    links.Add(body.Trim());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error parsing links from body: {ex.Message}");
            }
            return links.Distinct().ToList();
        }

        private void ShowNotification(string title, string message, bool isError)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CustomMessageBox.Show(message, title, isError ? CustomMessageBoxType.Error : CustomMessageBoxType.Success);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error showing notification: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            Stop();
            _pluginApiServer?.Dispose();
        }
    }
} 