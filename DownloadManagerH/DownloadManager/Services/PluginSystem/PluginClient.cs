using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Services.PluginSystem
{
    /// <summary>
    /// نمونه کلاینت برای اتصال به سرور Named Pipe DownloadManager
    /// افزونه‌ها می‌توانند از این کلاس برای ارتباط با DownloadManager استفاده کنند
    /// </summary>
    public class PluginClient : IDisposable
    {
        private readonly string _pipeName;
        private readonly ILogger _logger;
        private NamedPipeClientStream? _pipeClient;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _readTask;
        private bool _isConnected;
        private bool _disposed;
        private string? _pluginId;

        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler<ServerResponseEventArgs>? ResponseReceived;

        public PluginClient(string pipeName = "TrafficWatchPluginPipe", ILogger? logger = null)
        {
            _pipeName = pipeName;
            _logger = logger ?? LoggerFactory.GetDefaultLogger();
        }

        /// <summary>
        /// اتصال به سرور DownloadManager
        /// </summary>
        public async Task ConnectAsync()
        {
            if (_isConnected)
            {
                _logger.LogWarning("Already connected to DownloadManager");
                return;
            }

            try
            {
                _pipeClient = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                _logger.LogInfo($"Connecting to pipe: {_pipeName}...");

                await _pipeClient.ConnectAsync(5000);

                if (!_pipeClient.IsConnected)
                {
                    throw new TimeoutException("Failed to connect to DownloadManager");
                }

                _isConnected = true;
                _cancellationTokenSource = new CancellationTokenSource();
                _readTask = Task.Run(() => ReadResponsesAsync(_cancellationTokenSource.Token));

                _logger.LogInfo("Connected to DownloadManager successfully");
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to connect to DownloadManager", ex);
                _isConnected = false;
                throw;
            }
        }

        /// <summary>
        /// ثبت نام افزونه در DownloadManager
        /// </summary>
        public async Task<bool> RegisterAsync(string name, string version, string icon = "📦")
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Cannot register: not connected");
                return false;
            }

            var message = new
            {
                action = "register",
                name,
                version,
                icon
            };

            await SendMessageAsync(message);
            return true;
        }

        /// <summary>
        /// ارسال داده استریم به DownloadManager
        /// </summary>
        public async Task SendStreamDataAsync(object payload)
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Cannot send data: not connected");
                return;
            }

            var message = new
            {
                action = "stream_data",
                timestamp = DateTime.UtcNow.ToString("o"),
                payload
            };

            await SendMessageAsync(message);
        }

        /// <summary>
        /// ارسال Heartbeat به سرور
        /// </summary>
        public async Task SendHeartbeatAsync()
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Cannot send heartbeat: not connected");
                return;
            }

            var message = new { action = "heartbeat" };
            await SendMessageAsync(message);
        }

        private async Task SendMessageAsync(object message)
        {
            try
            {
                if (_pipeClient == null || !_pipeClient.IsConnected)
                {
                    _logger.LogWarning("Pipe not connected");
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

                var json = JsonSerializer.Serialize(message, options);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _pipeClient.WriteAsync(bytes, 0, bytes.Length);
                await _pipeClient.FlushAsync();

                _logger.LogDebug($"Message sent: {json}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending message", ex);
                throw;
            }
        }

        private async Task ReadResponsesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[4096];

                while (!cancellationToken.IsCancellationRequested && _pipeClient?.IsConnected == true)
                {
                    try
                    {
                        var bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                        if (bytesRead == 0)
                        {
                            _logger.LogDebug("Server closed connection");
                            break;
                        }

                        var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        _logger.LogDebug($"Response received: {json}");

                        try
                        {
                            var response = JsonDocument.Parse(json).RootElement;

                            if (response.TryGetProperty("action", out var actionElement))
                            {
                                var action = actionElement.GetString();

                                if (action == "registered" && response.TryGetProperty("id", out var idElement))
                                {
                                    _pluginId = idElement.GetString();
                                    _logger.LogInfo($"Registered with ID: {_pluginId}");
                                }

                                ResponseReceived?.Invoke(this, new ServerResponseEventArgs(response));
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning($"Invalid JSON response: {ex.Message}");
                        }
                    }
                    catch (IOException ex) when (ex.InnerException is OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Read task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error reading responses", ex);
            }
            finally
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// شروع حلقه Heartbeat خودکار (هر 30 ثانیه)
        /// </summary>
        public async Task StartHeartbeatLoopAsync()
        {
            while (_isConnected && !_disposed)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    await SendHeartbeatAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Heartbeat failed", ex);
                    break;
                }
            }
        }

        /// <summary>
        /// قطع اتصال از سرور
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _readTask?.Wait(TimeSpan.FromSeconds(2));
                _pipeClient?.Dispose();
                _isConnected = false;

                _logger.LogInfo("Disconnected from DownloadManager");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error disconnecting", ex);
            }
        }

        public string? GetPluginId() => _pluginId;
        public bool IsConnected() => _isConnected && _pipeClient?.IsConnected == true;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Disconnect();
                _cancellationTokenSource?.Dispose();
            }
        }
    }

    /// <summary>
    /// آرگومان‌های رویداد پاسخ سرور
    /// </summary>
    public class ServerResponseEventArgs : EventArgs
    {
        public JsonElement Response { get; }

        public ServerResponseEventArgs(JsonElement response)
        {
            Response = response;
        }
    }
}
