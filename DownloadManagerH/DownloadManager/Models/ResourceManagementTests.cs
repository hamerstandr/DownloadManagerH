using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// تست‌های عملکرد برای مدیریت منابع
    /// </summary>
    public class ResourceManagementTests
    {
        private readonly ILogger _logger;
        private readonly List<TestResult> _testResults;

        public ResourceManagementTests(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _testResults = new List<TestResult>();
        }

        /// <summary>
        /// اجرای تمام تست‌های عملکرد
        /// </summary>
        public async Task<TestSuite> RunAllTestsAsync()
        {
            _logger.LogInfo("شروع تست‌های عملکرد مدیریت منابع");
            var startTime = DateTime.Now;

            _testResults.Clear();

            // تست connection pool
            await TestConnectionPoolPerformanceAsync();
            await TestConnectionPoolConcurrencyAsync();
            await TestConnectionPoolMemoryUsageAsync();

            // تست resource manager
            await TestResourceManagerPerformanceAsync();
            await TestResourceManagerMemoryLeakDetectionAsync();
            await TestTempFileCleanupAsync();

            // تست memory monitor
            await TestMemoryMonitorAccuracyAsync();
            await TestMemoryCleanupEffectivenessAsync();
            await TestMemoryPressureDetectionAsync();

            var duration = DateTime.Now - startTime;
            var suite = new TestSuite
            {
                Name = "Resource Management Performance Tests",
                Duration = duration,
                Results = _testResults.ToList(),
                PassedCount = _testResults.Count(r => r.Passed),
                FailedCount = _testResults.Count(r => !r.Passed),
                TotalCount = _testResults.Count
            };

            _logger.LogInfo($"تست‌های عملکرد تکمیل شد: {suite.PassedCount}/{suite.TotalCount} موفق در {duration.TotalSeconds:F2} ثانیه");
            return suite;
        }

        /// <summary>
        /// تست عملکرد connection pool
        /// </summary>
        private async Task TestConnectionPoolPerformanceAsync()
        {
            var testName = "Connection Pool Performance";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var connectionPool = new ConnectionPoolManager(_logger);
                var tasks = new List<Task>();

                // ایجاد 100 درخواست همزمان
                for (int i = 0; i < 100; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        var client = await connectionPool.GetHttpClientAsync("test-host");
                        await Task.Delay(10); // شبیه‌سازی استفاده
                        connectionPool.ReturnHttpClient(client);
                    }));
                }

                await Task.WhenAll(tasks);
                stopwatch.Stop();

                var stats = connectionPool.GetStatistics();
                var passed = stopwatch.ElapsedMilliseconds < 5000 && stats.TotalConnections <= 10; // باید کمتر از 5 ثانیه و حداکثر 10 connection

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Duration: {stopwatch.ElapsedMilliseconds}ms, Connections: {stats.TotalConnections}",
                    Metrics = new Dictionary<string, object>
                    {
                        ["Duration"] = stopwatch.ElapsedMilliseconds,
                        ["TotalConnections"] = stats.TotalConnections,
                        ["UtilizationRate"] = stats.UtilizationRate
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست همزمانی connection pool
        /// </summary>
        private async Task TestConnectionPoolConcurrencyAsync()
        {
            var testName = "Connection Pool Concurrency";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var connectionPool = new ConnectionPoolManager(_logger);
                var concurrentTasks = 50;
                var tasks = new List<Task>();
                var successCount = 0;

                for (int i = 0; i < concurrentTasks; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var client = await connectionPool.GetHttpClientAsync($"host-{i % 5}");
                            await Task.Delay(100);
                            connectionPool.ReturnHttpClient(client);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Concurrent task failed: {ex.Message}");
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                stopwatch.Stop();

                var passed = successCount == concurrentTasks;

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Success: {successCount}/{concurrentTasks}",
                    Metrics = new Dictionary<string, object>
                    {
                        ["SuccessCount"] = successCount,
                        ["TotalTasks"] = concurrentTasks,
                        ["SuccessRate"] = (double)successCount / concurrentTasks * 100
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - {successCount}/{concurrentTasks}");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست استفاده از حافظه connection pool
        /// </summary>
        private async Task TestConnectionPoolMemoryUsageAsync()
        {
            var testName = "Connection Pool Memory Usage";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var initialMemory = GC.GetTotalMemory(true);
                var peakMemory = initialMemory;
                
                using (var connectionPool = new ConnectionPoolManager(_logger))
                {
                    var clients = new List<PooledHttpClient>();
                    
                    // ایجاد 20 connection
                    for (int i = 0; i < 20; i++)
                    {
                        var client = await connectionPool.GetHttpClientAsync($"host-{i}");
                        clients.Add(client);
                    }
                    
                    peakMemory = GC.GetTotalMemory(false);
                    
                    // بازگرداندن همه client ها
                    foreach (var client in clients)
                    {
                        connectionPool.ReturnHttpClient(client);
                    }
                    
                    // اجرای GC
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                
                var finalMemory = GC.GetTotalMemory(true);
                stopwatch.Stop();

                var memoryIncrease = finalMemory - initialMemory;
                var passed = memoryIncrease < 5 * 1024 * 1024; // کمتر از 5 MB افزایش حافظه

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Memory increase: {memoryIncrease / 1024}KB",
                    Metrics = new Dictionary<string, object>
                    {
                        ["InitialMemory"] = initialMemory,
                        ["PeakMemory"] = peakMemory,
                        ["FinalMemory"] = finalMemory,
                        ["MemoryIncrease"] = memoryIncrease
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - Memory increase: {memoryIncrease / 1024}KB");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست عملکرد resource manager
        /// </summary>
        private async Task TestResourceManagerPerformanceAsync()
        {
            var testName = "Resource Manager Performance";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var resourceManager = new ResourceManager(_logger);
                var resources = new List<IDisposable>();

                // ثبت 1000 منبع
                for (int i = 0; i < 1000; i++)
                {
                    var resource = new MemoryStream();
                    resources.Add(resource);
                    resourceManager.RegisterResource($"resource-{i}", resource);
                }

                // دریافت آمار
                var stats = resourceManager.GetStatistics();
                
                // حذف نیمی از منابع
                for (int i = 0; i < 500; i++)
                {
                    resourceManager.UnregisterResource($"resource-{i}");
                }

                stopwatch.Stop();

                var passed = stopwatch.ElapsedMilliseconds < 1000 && stats.ManagedResourceCount == 1000;

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Managed {stats.ManagedResourceCount} resources in {stopwatch.ElapsedMilliseconds}ms",
                    Metrics = new Dictionary<string, object>
                    {
                        ["Duration"] = stopwatch.ElapsedMilliseconds,
                        ["ManagedResources"] = stats.ManagedResourceCount,
                        ["TotalMemory"] = stats.TotalMemoryUsage
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست تشخیص نشت حافظه
        /// </summary>
        private async Task TestResourceManagerMemoryLeakDetectionAsync()
        {
            var testName = "Memory Leak Detection";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var resourceManager = new ResourceManager(_logger);
                var weakRefCount = 0;

                // ایجاد weak reference ها
                for (int i = 0; i < 100; i++)
                {
                    var obj = new object();
                    resourceManager.RegisterWeakReference($"weak-{i}", obj);
                    weakRefCount++;
                }

                // اجرای GC برای مرده کردن weak reference ها
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // صبر برای پاک‌سازی
                await Task.Delay(3000);

                var stats = resourceManager.GetStatistics();
                stopwatch.Stop();

                var passed = stats.DeadWeakReferences > 0; // باید weak reference های مرده تشخیص داده شوند

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Dead references: {stats.DeadWeakReferences}/{weakRefCount}",
                    Metrics = new Dictionary<string, object>
                    {
                        ["WeakRefCount"] = weakRefCount,
                        ["DeadWeakReferences"] = stats.DeadWeakReferences,
                        ["AliveWeakReferences"] = stats.AliveWeakReferences
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - Dead refs: {stats.DeadWeakReferences}");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست پاک‌سازی فایل‌های موقت
        /// </summary>
        private async Task TestTempFileCleanupAsync()
        {
            var testName = "Temp File Cleanup";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var resourceManager = new ResourceManager(_logger);
                var tempFiles = new List<string>();

                // ایجاد فایل‌های موقت
                for (int i = 0; i < 10; i++)
                {
                    var tempFile = Path.GetTempFileName();
                    File.WriteAllText(tempFile, $"Test content {i}");
                    tempFiles.Add(tempFile);
                    resourceManager.RegisterTempFile(tempFile);
                }

                // اجرای پاک‌سازی
                await resourceManager.CleanupTempFilesAsync();
                stopwatch.Stop();

                // بررسی حذف فایل‌ها
                var remainingFiles = tempFiles.Count(File.Exists);
                var passed = remainingFiles == 0;

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Cleaned {tempFiles.Count - remainingFiles}/{tempFiles.Count} temp files",
                    Metrics = new Dictionary<string, object>
                    {
                        ["TotalFiles"] = tempFiles.Count,
                        ["RemainingFiles"] = remainingFiles,
                        ["CleanedFiles"] = tempFiles.Count - remainingFiles
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - Cleaned {tempFiles.Count - remainingFiles} files");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست دقت memory monitor
        /// </summary>
        private async Task TestMemoryMonitorAccuracyAsync()
        {
            var testName = "Memory Monitor Accuracy";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var memoryMonitor = new MemoryMonitor(_logger);
                
                var snapshot1 = memoryMonitor.TakeSnapshot();
                
                // تخصیص حافظه
                var largeArray = new byte[10 * 1024 * 1024]; // 10 MB
                
                var snapshot2 = memoryMonitor.TakeSnapshot();
                stopwatch.Stop();

                var memoryIncrease = snapshot2.WorkingSet - snapshot1.WorkingSet;
                var passed = memoryIncrease > 0 && snapshot1 != null && snapshot2 != null;

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Memory increase detected: {memoryIncrease / 1024}KB",
                    Metrics = new Dictionary<string, object>
                    {
                        ["InitialMemory"] = snapshot1?.WorkingSet ?? 0,
                        ["FinalMemory"] = snapshot2?.WorkingSet ?? 0,
                        ["MemoryIncrease"] = memoryIncrease
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - Increase: {memoryIncrease / 1024}KB");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست اثربخشی پاک‌سازی حافظه
        /// </summary>
        private async Task TestMemoryCleanupEffectivenessAsync()
        {
            var testName = "Memory Cleanup Effectiveness";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var memoryMonitor = new MemoryMonitor(_logger);
                
                // تخصیص حافظه زیاد
                var arrays = new List<byte[]>();
                for (int i = 0; i < 100; i++)
                {
                    arrays.Add(new byte[1024 * 1024]); // 1 MB هر کدام
                }

                var beforeCleanup = memoryMonitor.TakeSnapshot();
                
                // حذف reference ها
                arrays.Clear();
                
                // اجرای پاک‌سازی
                var cleanupResult = await memoryMonitor.PerformCleanupAsync();
                
                var afterCleanup = memoryMonitor.TakeSnapshot();
                stopwatch.Stop();

                var memoryFreed = beforeCleanup.WorkingSet - afterCleanup.WorkingSet;
                var passed = cleanupResult.Success && memoryFreed > 0;

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Memory freed: {memoryFreed / 1024}KB in {cleanupResult.Duration.TotalMilliseconds}ms",
                    Metrics = new Dictionary<string, object>
                    {
                        ["MemoryFreed"] = memoryFreed,
                        ["CleanupDuration"] = cleanupResult.Duration.TotalMilliseconds,
                        ["CleanupSuccess"] = cleanupResult.Success
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - Freed: {memoryFreed / 1024}KB");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }

        /// <summary>
        /// تست تشخیص فشار حافظه
        /// </summary>
        private async Task TestMemoryPressureDetectionAsync()
        {
            var testName = "Memory Pressure Detection";
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var memoryMonitor = new MemoryMonitor(_logger);
                memoryMonitor.WarningThreshold = 50 * 1024 * 1024; // 50 MB threshold برای تست
                
                var pressureDetected = false;
                memoryMonitor.MemoryPressureDetected += (s, e) => pressureDetected = true;

                // تخصیص حافظه زیاد برای trigger کردن فشار حافظه
                var largeArrays = new List<byte[]>();
                for (int i = 0; i < 100; i++)
                {
                    largeArrays.Add(new byte[1024 * 1024]); // 1 MB
                }

                // صبر برای تشخیص فشار حافظه
                await Task.Delay(2000);
                
                stopwatch.Stop();

                var passed = pressureDetected;

                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = passed,
                    Duration = stopwatch.Elapsed,
                    Details = $"Memory pressure detected: {pressureDetected}",
                    Metrics = new Dictionary<string, object>
                    {
                        ["PressureDetected"] = pressureDetected,
                        ["AllocatedMemory"] = largeArrays.Count * 1024 * 1024
                    }
                });

                _logger.LogInfo($"{testName}: {(passed ? "PASSED" : "FAILED")} - Pressure detected: {pressureDetected}");
            }
            catch (Exception ex)
            {
                _testResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message
                });
                _logger.LogError($"{testName} failed", ex);
            }
        }
    }

    #region Test Data Models

    public class TestSuite
    {
        public string Name { get; set; }
        public TimeSpan Duration { get; set; }
        public List<TestResult> Results { get; set; } = new();
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public int TotalCount { get; set; }
        
        public double SuccessRate => TotalCount > 0 ? (double)PassedCount / TotalCount * 100 : 0;
    }

    public class TestResult
    {
        public string Name { get; set; }
        public bool Passed { get; set; }
        public TimeSpan Duration { get; set; }
        public string Details { get; set; }
        public string Error { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    #endregion
}