using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using DownloadManagerH.Models.Logging;
using DownloadManagerH.Windows;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Native Messaging Host implementation for browser extension communication
    /// Handles stdin/stdout JSON message protocol according to Chrome Native Messaging specification
    /// </summary>
    public class NativeMessagingHost : INativeMessagingHost, IDisposable
    {
        private readonly DownloadManager _downloadManager;
        private readonly ILogger _logger;
        private readonly DownloadInterceptionManager _interceptionManager;
        private readonly PriorityMessageQueue _priorityQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _sendLock = new object();
        
        private Task? _listeningTask;
        private bool _isRunning = false;
        private bool _disposed = false;

        // Native Messaging protocol constants
        private const int MAX_MESSAGE_SIZE = 1024 * 1024; // 1MB max message size
        private const int MESSAGE_LENGTH_BYTES = 4; // 4 bytes for message length prefix

        public bool IsRunning => _isRunning && !_disposed;

        public event EventHandler<NativeMessageEventArgs>? MessageReceived;
        public event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;
        public event EventHandler<NativeMessagingErrorEventArgs>? ErrorOccurred;

        public NativeMessagingHost(DownloadManager downloadManager, ILogger logger)
        {
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _interceptionManager = new DownloadInterceptionManager(_downloadManager, _logger);
            _priorityQueue = new PriorityMessageQueue(_logger);
            _cancellationTokenSource = new CancellationTokenSource();

            // Subscribe to interception events
            _interceptionManager.DownloadIntercepted += OnDownloadIntercepted;
            _interceptionManager.DownloadProcessed += OnDownloadProcessed;

            // Subscribe to priority queue events
            _priorityQueue.MessageProcessed += OnPriorityMessageProcessed;
            _priorityQueue.MessageQueued += OnPriorityMessageQueued;

            _logger.LogInfo("Native Messaging Host initialized with Download Interception Manager and Priority Queue");
        }

        public async Task StartAsync()
        {
            if (_isRunning || _disposed)
            {
                _logger.LogWarning("Native Messaging Host already running or disposed");
                return;
            }

            try
            {
                _isRunning = true;
                
                // Start the message listening loop
                _listeningTask = Task.Run(MessageListeningLoop, _cancellationTokenSource.Token);
                
                _logger.LogInfo("Native Messaging Host started successfully");
                OnConnectionStateChanged(true, "unknown", "Native Messaging Host started");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.LogError("Failed to start Native Messaging Host", ex);
                OnErrorOccurred(ex, "StartupError");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning || _disposed)
            {
                return;
            }

            try
            {
                _isRunning = false;
                _cancellationTokenSource.Cancel();

                if (_listeningTask != null)
                {
                    await _listeningTask;
                }

                _logger.LogInfo("Native Messaging Host stopped");
                OnConnectionStateChanged(false, "unknown", "Native Messaging Host stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error stopping Native Messaging Host", ex);
                OnErrorOccurred(ex, "ShutdownError");
            }
        }

        public async Task SendMessageAsync(object message)
        {
            if (!_isRunning || _disposed)
            {
                throw new InvalidOperationException("Native Messaging Host is not running");
            }

            try
            {
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                await SendRawMessageAsync(json);
                
                _logger.LogDebug($"Sent message: {message.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending message: {message.GetType().Name}", ex);
                OnErrorOccurred(ex, "SendError");
                throw;
            }
        }

        private async Task SendRawMessageAsync(string jsonMessage)
        {
            var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
            
            if (messageBytes.Length > MAX_MESSAGE_SIZE)
            {
                throw new InvalidOperationException($"Message too large: {messageBytes.Length} bytes (max: {MAX_MESSAGE_SIZE})");
            }

            lock (_sendLock)
            {
                try
                {
                    // Write message length (4 bytes, little-endian)
                    var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthBytes);
                    }
                    
                    Console.OpenStandardOutput().Write(lengthBytes, 0, MESSAGE_LENGTH_BYTES);
                    
                    // Write message content
                    Console.OpenStandardOutput().Write(messageBytes, 0, messageBytes.Length);
                    Console.OpenStandardOutput().Flush();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error writing to stdout", ex);
                    throw;
                }
            }
        }

        private async Task MessageListeningLoop()
        {
            _logger.LogInfo("Started Native Messaging listening loop");

            try
            {
                using var stdin = Console.OpenStandardInput();
                
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Read message length (4 bytes)
                        var lengthBuffer = new byte[MESSAGE_LENGTH_BYTES];
                        var bytesRead = await ReadExactAsync(stdin, lengthBuffer, MESSAGE_LENGTH_BYTES, _cancellationTokenSource.Token);
                        
                        if (bytesRead == 0)
                        {
                            // EOF reached, browser closed connection
                            _logger.LogInfo("Browser closed connection (EOF)");
                            break;
                        }

                        if (bytesRead != MESSAGE_LENGTH_BYTES)
                        {
                            _logger.LogWarning($"Invalid message length header: expected {MESSAGE_LENGTH_BYTES} bytes, got {bytesRead}");
                            continue;
                        }

                        // Convert length bytes to integer (little-endian)
                        if (!BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(lengthBuffer);
                        }
                        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                        // Validate message length
                        if (messageLength <= 0 || messageLength > MAX_MESSAGE_SIZE)
                        {
                            _logger.LogWarning($"Invalid message length: {messageLength}");
                            continue;
                        }

                        // Read message content
                        var messageBuffer = new byte[messageLength];
                        bytesRead = await ReadExactAsync(stdin, messageBuffer, messageLength, _cancellationTokenSource.Token);
                        
                        if (bytesRead != messageLength)
                        {
                            _logger.LogWarning($"Incomplete message: expected {messageLength} bytes, got {bytesRead}");
                            continue;
                        }

                        // Parse and process message
                        var jsonMessage = Encoding.UTF8.GetString(messageBuffer);
                        await ProcessReceivedMessage(jsonMessage);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error in message listening loop", ex);
                        OnErrorOccurred(ex, "ListeningError");
                        
                        // Brief delay before retrying to avoid tight error loops
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                _logger.LogDebug("Message listening loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError("Fatal error in message listening loop", ex);
                OnErrorOccurred(ex, "FatalListeningError");
            }
            finally
            {
                _logger.LogInfo("Native Messaging listening loop ended");
            }
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            var totalBytesRead = 0;
            
            while (totalBytesRead < count)
            {
                var bytesRead = await stream.ReadAsync(buffer, totalBytesRead, count - totalBytesRead, cancellationToken);
                
                if (bytesRead == 0)
                {
                    // EOF reached
                    break;
                }
                
                totalBytesRead += bytesRead;
            }
            
            return totalBytesRead;
        }

        private async Task ProcessReceivedMessage(string jsonMessage)
        {
            try
            {
                _logger.LogDebug($"Received message: {jsonMessage}");

                // Parse base message to determine type
                var baseMessage = JsonSerializer.Deserialize<NativeMessage>(jsonMessage, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (baseMessage == null)
                {
                    _logger.LogWarning("Received null message");
                    return;
                }

                // Determine browser from message or process info
                var browser = DetermineBrowser(baseMessage.Browser);
                baseMessage.Browser = browser;

                // Fire message received event
                OnMessageReceived(baseMessage, browser);

                // Check if priority queue is enabled
                if (_interceptionManager.Settings.EnablePriorityQueue)
                {
                    // Add message to priority queue for processing
                    _priorityQueue.EnqueueMessage(baseMessage, async (msg) =>
                    {
                        await HandleMessageByType(msg, jsonMessage, browser);
                        return new { success = true };
                    });
                }
                else
                {
                    // Process message directly
                    await HandleMessageByType(baseMessage, jsonMessage, browser);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning($"Invalid JSON message received: {ex.Message}");
                OnErrorOccurred(ex, "JsonParseError");
                
                // Send error response
                await SendErrorResponse("Invalid JSON format", "JsonParseError");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing received message", ex);
                OnErrorOccurred(ex, "MessageProcessingError");
                
                // Send error response
                await SendErrorResponse("Error processing message", "ProcessingError");
            }
        }

        private async Task HandleMessageByType(NativeMessage baseMessage, string originalJson, string browser)
        {
            switch (baseMessage.Type?.ToLower())
            {
                case "adddownload":
                    await HandleAddDownloadMessage(originalJson, browser);
                    break;

                case "getstatus":
                    await HandleStatusRequest(browser);
                    break;

                case "getsettings":
                    await HandleSettingsRequest(browser);
                    break;

                case "focus":
                    await HandleFocusRequest(browser);
                    break;

                case "interceptdownload":
                    await HandleInterceptDownloadMessage(originalJson, browser);
                    break;

                default:
                    _logger.LogWarning($"Unknown message type: {baseMessage.Type}");
                    await SendErrorResponse($"Unknown message type: {baseMessage.Type}", "UnknownMessageType");
                    break;
            }
        }

        private async Task HandleAddDownloadMessage(string jsonMessage, string browser)
        {
            try
            {
                var message = JsonSerializer.Deserialize<AddDownloadMessage>(jsonMessage, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (message?.Data?.Links == null || message.Data.Links.Count == 0)
                {
                    await SendErrorResponse("No download links provided", "InvalidRequest");
                    return;
                }

                // Set browser info
                message.Browser = browser;

                // Process downloads using DownloadInterceptionManager
                var result = await _interceptionManager.ProcessAddDownloadMessage(message);

                var response = new ResponseMessage
                {
                    Browser = browser,
                    Data = new ResponseData
                    {
                        Success = result.Success,
                        Message = result.Message,
                        AddedCount = result.ProcessedCount,
                        ErrorCount = result.ErrorCount,
                        Errors = result.Errors,
                        AddedUrls = result.ProcessedUrls
                    }
                };

                await SendMessageAsync(response);
                _logger.LogInfo($"Processed add download request: {result.ProcessedCount} successful, {result.ErrorCount} errors from {browser}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling add download message", ex);
                await SendErrorResponse("Error processing download request", "DownloadProcessingError");
            }
        }

        private async Task HandleStatusRequest(string browser)
        {
            try
            {
                var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                
                var response = new StatusResponseMessage
                {
                    Browser = browser,
                    Data = new StatusData
                    {
                        Status = "running",
                        ActiveDownloads = _downloadManager.GetActiveDownloadsCount(),
                        TotalDownloads = _downloadManager.GetTotalDownloadsCount(),
                        Version = "1.0",
                        Uptime = uptime
                    }
                };

                await SendMessageAsync(response);
                _logger.LogDebug($"Sent status response to {browser}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling status request", ex);
                await SendErrorResponse("Error getting status", "StatusError");
            }
        }

        private async Task HandleSettingsRequest(string browser)
        {
            try
            {
                var response = new SettingsResponseMessage
                {
                    Browser = browser,
                    Data = new SettingsData
                    {
                        DefaultDownloadPath = Settings.DefaultDownloadPath,
                        AddDownloadsDirectly = Settings.AddDownloadsDirectly,
                        MaxConcurrentDownloads = Settings.MaxConcurrentDownloadsLimit,
                        EnableClipboardMonitoring = Settings.MonitorClipboard,
                        EnableDownloadInterception = true // Will be configurable in future
                    }
                };

                await SendMessageAsync(response);
                _logger.LogDebug($"Sent settings response to {browser}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling settings request", ex);
                await SendErrorResponse("Error getting settings", "SettingsError");
            }
        }

        private async Task HandleFocusRequest(string browser)
        {
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

                var response = new ResponseMessage
                {
                    Browser = browser,
                    Data = new ResponseData
                    {
                        Success = true,
                        Message = "Window focused successfully"
                    }
                };

                await SendMessageAsync(response);
                _logger.LogInfo($"Focused main window via {browser}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling focus request", ex);
                await SendErrorResponse("Error focusing window", "FocusError");
            }
        }

        private async Task HandleInterceptDownloadMessage(string jsonMessage, string browser)
        {
            try
            {
                var message = JsonSerializer.Deserialize<InterceptDownloadMessage>(jsonMessage, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (message?.Data == null)
                {
                    await SendErrorResponse("Invalid intercept download data", "InvalidRequest");
                    return;
                }

                // Set browser info
                message.Browser = browser;

                // Process download interception using DownloadInterceptionManager
                var result = await _interceptionManager.ProcessInterceptDownloadMessage(message);

                var response = new ResponseMessage
                {
                    Browser = browser,
                    Data = new ResponseData
                    {
                        Success = result.Success,
                        Message = result.Message,
                        AddedCount = result.ProcessedCount,
                        ErrorCount = result.ErrorCount,
                        Errors = result.Errors,
                        AddedUrls = result.ProcessedUrls
                    }
                };

                await SendMessageAsync(response);
                _logger.LogInfo($"Processed download interception from {browser}: {result.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling intercept download message", ex);
                await SendErrorResponse("Error processing download interception", "InterceptionError");
            }
        }

        private async Task SendErrorResponse(string errorMessage, string errorType)
        {
            try
            {
                var response = new ResponseMessage
                {
                    Data = new ResponseData
                    {
                        Success = false,
                        Message = errorMessage,
                        Errors = [errorMessage]
                    }
                };

                await SendMessageAsync(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending error response: {errorMessage}", ex);
            }
        }

        private static string DetermineBrowser(string browserFromMessage)
        {
            if (!string.IsNullOrEmpty(browserFromMessage) && browserFromMessage != "unknown")
            {
                return browserFromMessage;
            }

            try
            {
                // Try to determine browser from parent process
                var currentProcess = Process.GetCurrentProcess();
                var parentProcess = GetParentProcess(currentProcess);
                
                if (parentProcess != null)
                {
                    var processName = parentProcess.ProcessName.ToLower();
                    
                    if (processName.Contains("chrome"))
                        return "chrome";
                    else if (processName.Contains("msedge"))
                        return "edge";
                    else if (processName.Contains("firefox"))
                        return "firefox";
                }
            }
            catch (Exception)
            {
                // Ignore errors in browser detection
            }

            return "unknown";
        }

        private static Process? GetParentProcess(Process process)
        {
            try
            {
                var parentId = GetParentProcessId(process.Id);
                return parentId > 0 ? Process.GetProcessById(parentId) : null;
            }
            catch
            {
                return null;
            }
        }

        private static int GetParentProcessId(int processId)
        {
            // This is a simplified implementation
            // In a full implementation, you would use WMI or P/Invoke to get the parent process ID
            return 0;
        }

        protected virtual void OnMessageReceived(NativeMessage message, string browser)
        {
            MessageReceived?.Invoke(this, new NativeMessageEventArgs(message, browser));
        }

        protected virtual void OnConnectionStateChanged(bool isConnected, string browser, string message)
        {
            ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs(isConnected, browser, message));
        }

        protected virtual void OnErrorOccurred(Exception exception, string errorType)
        {
            ErrorOccurred?.Invoke(this, new NativeMessagingErrorEventArgs(exception, errorType));
        }

        protected virtual void OnErrorOccurred(string message, string errorType)
        {
            ErrorOccurred?.Invoke(this, new NativeMessagingErrorEventArgs(message, errorType));
        }

        /// <summary>
        /// Event handler for download interception
        /// </summary>
        private void OnDownloadIntercepted(object? sender, DownloadInterceptedEventArgs e)
        {
            _logger.LogInfo($"Download intercepted from {e.Browser}: {e.DownloadItem.FileName} (Priority: {e.Priority})");
            
            // اختیاری: ارسال اعلان به کاربر
            if (_interceptionManager.Settings.ShowInterceptionNotification)
            {
                // Implementation for showing notification can be added here
            }
        }

        /// <summary>
        /// Event handler for download processing
        /// </summary>
        private void OnDownloadProcessed(object? sender, DownloadProcessedEventArgs e)
        {
            _logger.LogDebug($"Download processed from {e.Browser}: {e.DownloadItem.FileName} (Priority: {e.Priority})");
        }

        /// <summary>
        /// Event handler for priority queue message processing
        /// </summary>
        private void OnPriorityMessageProcessed(object? sender, MessageProcessedEventArgs e)
        {
            if (e.Success)
            {
                _logger.LogDebug($"Priority message processed successfully: {e.Message.Type} from {e.Message.Browser} (Priority: {e.Priority}, Wait: {e.WaitTime.TotalMilliseconds:F0}ms, Process: {e.ProcessingTime.TotalMilliseconds:F0}ms)");
            }
            else
            {
                _logger.LogWarning($"Priority message processing failed: {e.Message.Type} from {e.Message.Browser} - {e.Exception?.Message}");
            }
        }

        /// <summary>
        /// Event handler for priority queue message queuing
        /// </summary>
        private void OnPriorityMessageQueued(object? sender, MessageQueuedEventArgs e)
        {
            _logger.LogDebug($"Message queued with {e.Priority} priority: {e.Message.Type} from {e.Message.Browser} (Queue size: {e.QueueSize})");
        }

        /// <summary>
        /// Get download interception manager for external access
        /// </summary>
        public DownloadInterceptionManager GetInterceptionManager()
        {
            return _interceptionManager;
        }

        /// <summary>
        /// Get priority message queue for external access
        /// </summary>
        public PriorityMessageQueue GetPriorityQueue()
        {
            return _priorityQueue;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                // Unsubscribe from events
                if (_interceptionManager != null)
                {
                    _interceptionManager.DownloadIntercepted -= OnDownloadIntercepted;
                    _interceptionManager.DownloadProcessed -= OnDownloadProcessed;
                }

                if (_priorityQueue != null)
                {
                    _priorityQueue.MessageProcessed -= OnPriorityMessageProcessed;
                    _priorityQueue.MessageQueued -= OnPriorityMessageQueued;
                    _priorityQueue.Dispose();
                }

                StopAsync().Wait(5000); // Wait up to 5 seconds for graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during disposal", ex);
            }

            _cancellationTokenSource?.Dispose();
        }
    }
}