using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// صف اولویت‌دار برای پردازش پیام‌های Native Messaging
    /// </summary>
    public class PriorityMessageQueue : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<NativeMessagingProtocol.BrowserPriority, ConcurrentQueue<QueuedMessage>> _queues;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processingTask;
        private readonly object _statsLock = new object();
        
        private bool _disposed = false;
        private PriorityQueueStats _stats;

        public event EventHandler<MessageProcessedEventArgs>? MessageProcessed;
        public event EventHandler<MessageQueuedEventArgs>? MessageQueued;

        public PriorityMessageQueue(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queues = new ConcurrentDictionary<NativeMessagingProtocol.BrowserPriority, ConcurrentQueue<QueuedMessage>>();
            _semaphore = new SemaphoreSlim(0);
            _cancellationTokenSource = new CancellationTokenSource();
            _stats = new PriorityQueueStats();

            // مقداردهی صف‌ها برای هر اولویت
            foreach (var priority in Enum.GetValues<NativeMessagingProtocol.BrowserPriority>())
            {
                _queues[priority] = new ConcurrentQueue<QueuedMessage>();
            }

            // شروع task پردازش پیام‌ها
            _processingTask = Task.Run(ProcessMessagesAsync, _cancellationTokenSource.Token);
            
            _logger.LogInfo("Priority Message Queue initialized");
        }

        /// <summary>
        /// اضافه کردن پیام به صف با اولویت
        /// </summary>
        public void EnqueueMessage(NativeMessage message, Func<NativeMessage, Task<object>> processor)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PriorityMessageQueue));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (processor == null)
                throw new ArgumentNullException(nameof(processor));

            var priority = message.GetBrowserPriority();
            var queuedMessage = new QueuedMessage
            {
                Message = message,
                Processor = processor,
                QueuedTime = DateTime.UtcNow,
                Priority = priority
            };

            _queues[priority].Enqueue(queuedMessage);

            // آپدیت آمار
            lock (_statsLock)
            {
                _stats.TotalQueued++;
                _stats.QueuedByPriority[priority] = _stats.QueuedByPriority.GetValueOrDefault(priority, 0) + 1;
                _stats.QueuedByBrowser[message.Browser] = _stats.QueuedByBrowser.GetValueOrDefault(message.Browser, 0) + 1;
            }

            // اطلاع‌رسانی وجود پیام جدید
            _semaphore.Release();

            OnMessageQueued(new MessageQueuedEventArgs
            {
                Message = message,
                Priority = priority,
                QueueSize = GetQueueSize(priority)
            });

            _logger.LogDebug($"Message queued with {priority} priority from {message.Browser}: {message.Type}");
        }

        /// <summary>
        /// پردازش پیام‌ها بر اساس اولویت
        /// </summary>
        private async Task ProcessMessagesAsync()
        {
            _logger.LogInfo("Started priority message processing");

            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await _semaphore.WaitAsync(_cancellationTokenSource.Token);

                    var message = DequeueHighestPriorityMessage();
                    if (message != null)
                    {
                        await ProcessSingleMessage(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Priority message processing cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in priority message processing", ex);
            }
            finally
            {
                _logger.LogInfo("Priority message processing ended");
            }
        }

        /// <summary>
        /// دریافت پیام با بالاترین اولویت از صف
        /// </summary>
        private QueuedMessage? DequeueHighestPriorityMessage()
        {
            // بررسی صف‌ها از بالاترین اولویت به پایین‌ترین
            var priorities = Enum.GetValues<NativeMessagingProtocol.BrowserPriority>()
                .OrderByDescending(p => (int)p);

            foreach (var priority in priorities)
            {
                if (_queues[priority].TryDequeue(out var message))
                {
                    return message;
                }
            }

            return null;
        }

        /// <summary>
        /// پردازش یک پیام
        /// </summary>
        private async Task ProcessSingleMessage(QueuedMessage queuedMessage)
        {
            var startTime = DateTime.UtcNow;
            var success = false;
            Exception? exception = null;

            try
            {
                var result = await queuedMessage.Processor(queuedMessage.Message);
                success = true;

                _logger.LogDebug($"Successfully processed {queuedMessage.Priority} priority message from {queuedMessage.Message.Browser}");
            }
            catch (Exception ex)
            {
                exception = ex;
                _logger.LogError($"Error processing {queuedMessage.Priority} priority message from {queuedMessage.Message.Browser}", ex);
            }
            finally
            {
                var processingTime = DateTime.UtcNow - startTime;
                var waitTime = startTime - queuedMessage.QueuedTime;

                // آپدیت آمار
                lock (_statsLock)
                {
                    _stats.TotalProcessed++;
                    if (success)
                    {
                        _stats.TotalSuccessful++;
                    }
                    else
                    {
                        _stats.TotalFailed++;
                    }

                    _stats.ProcessedByPriority[queuedMessage.Priority] = 
                        _stats.ProcessedByPriority.GetValueOrDefault(queuedMessage.Priority, 0) + 1;
                    
                    _stats.ProcessedByBrowser[queuedMessage.Message.Browser] = 
                        _stats.ProcessedByBrowser.GetValueOrDefault(queuedMessage.Message.Browser, 0) + 1;

                    // آپدیت میانگین زمان‌ها
                    _stats.AverageWaitTime = UpdateAverage(_stats.AverageWaitTime, waitTime, _stats.TotalProcessed);
                    _stats.AverageProcessingTime = UpdateAverage(_stats.AverageProcessingTime, processingTime, _stats.TotalProcessed);
                }

                OnMessageProcessed(new MessageProcessedEventArgs
                {
                    Message = queuedMessage.Message,
                    Priority = queuedMessage.Priority,
                    Success = success,
                    Exception = exception,
                    ProcessingTime = processingTime,
                    WaitTime = waitTime
                });
            }
        }

        /// <summary>
        /// به‌روزرسانی میانگین
        /// </summary>
        private static TimeSpan UpdateAverage(TimeSpan currentAverage, TimeSpan newValue, int count)
        {
            if (count <= 1)
                return newValue;

            var totalTicks = (currentAverage.Ticks * (count - 1)) + newValue.Ticks;
            return new TimeSpan(totalTicks / count);
        }

        /// <summary>
        /// دریافت اندازه صف برای اولویت خاص
        /// </summary>
        public int GetQueueSize(NativeMessagingProtocol.BrowserPriority priority)
        {
            return _queues[priority].Count;
        }

        /// <summary>
        /// دریافت اندازه کل صف‌ها
        /// </summary>
        public int GetTotalQueueSize()
        {
            return _queues.Values.Sum(q => q.Count);
        }

        /// <summary>
        /// دریافت آمار صف
        /// </summary>
        public PriorityQueueStats GetStats()
        {
            lock (_statsLock)
            {
                return new PriorityQueueStats
                {
                    TotalQueued = _stats.TotalQueued,
                    TotalProcessed = _stats.TotalProcessed,
                    TotalSuccessful = _stats.TotalSuccessful,
                    TotalFailed = _stats.TotalFailed,
                    QueuedByPriority = new Dictionary<NativeMessagingProtocol.BrowserPriority, int>(_stats.QueuedByPriority),
                    ProcessedByPriority = new Dictionary<NativeMessagingProtocol.BrowserPriority, int>(_stats.ProcessedByPriority),
                    QueuedByBrowser = new Dictionary<string, int>(_stats.QueuedByBrowser),
                    ProcessedByBrowser = new Dictionary<string, int>(_stats.ProcessedByBrowser),
                    AverageWaitTime = _stats.AverageWaitTime,
                    AverageProcessingTime = _stats.AverageProcessingTime,
                    StartTime = _stats.StartTime
                };
            }
        }

        /// <summary>
        /// ریست آمار
        /// </summary>
        public void ResetStats()
        {
            lock (_statsLock)
            {
                _stats = new PriorityQueueStats();
                _logger.LogInfo("Priority queue stats reset");
            }
        }

        /// <summary>
        /// پاک کردن تمام صف‌ها
        /// </summary>
        public void Clear()
        {
            foreach (var queue in _queues.Values)
            {
                while (queue.TryDequeue(out _)) { }
            }

            lock (_statsLock)
            {
                _stats = new PriorityQueueStats();
            }

            _logger.LogInfo("Priority message queues cleared");
        }

        protected virtual void OnMessageQueued(MessageQueuedEventArgs e)
        {
            MessageQueued?.Invoke(this, e);
        }

        protected virtual void OnMessageProcessed(MessageProcessedEventArgs e)
        {
            MessageProcessed?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _cancellationTokenSource.Cancel();
                _processingTask.Wait(5000); // Wait up to 5 seconds
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during priority queue disposal", ex);
            }

            _cancellationTokenSource.Dispose();
            _semaphore.Dispose();
        }
    }

    /// <summary>
    /// پیام در صف
    /// </summary>
    public class QueuedMessage
    {
        public NativeMessage Message { get; set; } = null!;
        public Func<NativeMessage, Task<object>> Processor { get; set; } = null!;
        public DateTime QueuedTime { get; set; }
        public NativeMessagingProtocol.BrowserPriority Priority { get; set; }
    }

    /// <summary>
    /// آمار صف اولویت‌دار
    /// </summary>
    public class PriorityQueueStats
    {
        public int TotalQueued { get; set; } = 0;
        public int TotalProcessed { get; set; } = 0;
        public int TotalSuccessful { get; set; } = 0;
        public int TotalFailed { get; set; } = 0;
        
        public Dictionary<NativeMessagingProtocol.BrowserPriority, int> QueuedByPriority { get; set; } = new();
        public Dictionary<NativeMessagingProtocol.BrowserPriority, int> ProcessedByPriority { get; set; } = new();
        public Dictionary<string, int> QueuedByBrowser { get; set; } = new();
        public Dictionary<string, int> ProcessedByBrowser { get; set; } = new();
        
        public TimeSpan AverageWaitTime { get; set; } = TimeSpan.Zero;
        public TimeSpan AverageProcessingTime { get; set; } = TimeSpan.Zero;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        public double SuccessRate => TotalProcessed > 0 ? (double)TotalSuccessful / TotalProcessed * 100 : 0;
        public int PendingMessages => TotalQueued - TotalProcessed;
    }

    /// <summary>
    /// Event Args برای پیام اضافه شده به صف
    /// </summary>
    public class MessageQueuedEventArgs : EventArgs
    {
        public NativeMessage Message { get; set; } = null!;
        public NativeMessagingProtocol.BrowserPriority Priority { get; set; }
        public int QueueSize { get; set; }
    }

    /// <summary>
    /// Event Args برای پیام پردازش شده
    /// </summary>
    public class MessageProcessedEventArgs : EventArgs
    {
        public NativeMessage Message { get; set; } = null!;
        public NativeMessagingProtocol.BrowserPriority Priority { get; set; }
        public bool Success { get; set; }
        public Exception? Exception { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public TimeSpan WaitTime { get; set; }
    }
}