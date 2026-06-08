using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Reflection;
using System.Collections.Generic;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Manages Native Messaging host registration in Windows registry for Chrome, Edge, and Firefox
    /// </summary>
    public class NativeMessagingRegistrar : INativeMessagingRegistrar, IDisposable
    {
        private readonly ILogger _logger;
        private readonly NativeMessagingConfiguration _configuration;
        private readonly string _executablePath;
        private readonly string _manifestsDirectory;
        private bool _disposed = false;

        // Registry paths for different browsers
        private static readonly Dictionary<string, string> BrowserRegistryPaths = new()
        {
            ["chrome"] = @"SOFTWARE\Google\Chrome\NativeMessagingHosts",
            ["edge"] = @"SOFTWARE\Microsoft\Edge\NativeMessagingHosts", 
            ["firefox"] = @"SOFTWARE\Mozilla\NativeMessagingHosts"
        };

        public bool IsRegistered => CheckRegistrationStatus();
        public string? RegisteredHostPath => GetRegisteredHostPath();

        public event EventHandler<RegistrationStateEventArgs>? RegistrationStateChanged;
        public event EventHandler<RegistrationErrorEventArgs>? RegistrationError;

        public NativeMessagingRegistrar(ILogger logger, NativeMessagingConfiguration? configuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? new NativeMessagingConfiguration(logger);
            
            // Get the current executable path
            _executablePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(_executablePath))
            {
                _executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine executable path");
            }

            // Create manifests directory next to executable
            var executableDir = Path.GetDirectoryName(_executablePath) ?? throw new InvalidOperationException("Cannot determine executable directory");
            _manifestsDirectory = Path.Combine(executableDir, "NativeMessagingManifests");

            _logger.LogInfo($"Native Messaging Registrar initialized for host: {_configuration.HostName}");
            _logger.LogInfo($"Executable path: {_executablePath}");
            _logger.LogInfo($"Manifests directory: {_manifestsDirectory}");
        }

        public async Task RegisterHostAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativeMessagingRegistrar));

            try
            {
                _logger.LogInfo("Starting Native Messaging host registration");

                // Create manifests directory if it doesn't exist
                Directory.CreateDirectory(_manifestsDirectory);

                // Register for each browser
                var registrationTasks = new List<Task>();
                
                foreach (var browser in BrowserRegistryPaths.Keys)
                {
                    registrationTasks.Add(RegisterForBrowserAsync(browser));
                }

                await Task.WhenAll(registrationTasks);

                _logger.LogInfo("Native Messaging host registration completed successfully");
                OnRegistrationStateChanged(true, "all", "Registration completed for all browsers");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to register Native Messaging host", ex);
                OnRegistrationError(ex, "all", "register");
                throw;
            }
        }

        public async Task UnregisterHostAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativeMessagingRegistrar));

            try
            {
                _logger.LogInfo("Starting Native Messaging host unregistration");

                // Unregister from each browser
                var unregistrationTasks = new List<Task>();
                
                foreach (var browser in BrowserRegistryPaths.Keys)
                {
                    unregistrationTasks.Add(UnregisterFromBrowserAsync(browser));
                }

                await Task.WhenAll(unregistrationTasks);

                // Clean up manifest files
                try
                {
                    if (Directory.Exists(_manifestsDirectory))
                    {
                        Directory.Delete(_manifestsDirectory, true);
                        _logger.LogInfo("Cleaned up manifest files");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to clean up manifest files", ex);
                }

                _logger.LogInfo("Native Messaging host unregistration completed");
                OnRegistrationStateChanged(false, "all", "Unregistration completed for all browsers");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to unregister Native Messaging host", ex);
                OnRegistrationError(ex, "all", "unregister");
                throw;
            }
        }

        public async Task UpdateHostPathAsync(string newPath)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativeMessagingRegistrar));

            if (string.IsNullOrEmpty(newPath) || !File.Exists(newPath))
                throw new ArgumentException("Invalid executable path", nameof(newPath));

            try
            {
                _logger.LogInfo($"Updating Native Messaging host path to: {newPath}");

                // Update manifests with new path
                foreach (var browser in BrowserRegistryPaths.Keys)
                {
                    await CreateManifestFileAsync(browser, newPath);
                }

                _logger.LogInfo("Native Messaging host path updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update host path to: {newPath}", ex);
                OnRegistrationError(ex, "all", "update");
                throw;
            }
        }

        private async Task RegisterForBrowserAsync(string browser)
        {
            try
            {
                _logger.LogInfo($"Registering Native Messaging host for {browser}");

                // Create manifest file for this browser
                var manifestPath = await CreateManifestFileAsync(browser, _executablePath);

                // Register in Windows registry
                await RegisterInRegistryAsync(browser, manifestPath);

                _logger.LogInfo($"Successfully registered for {browser}");
                OnRegistrationStateChanged(true, browser, "Registration successful");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to register for {browser}", ex);
                OnRegistrationError(ex, browser, "register");
                throw;
            }
        }

        private async Task UnregisterFromBrowserAsync(string browser)
        {
            try
            {
                _logger.LogInfo($"Unregistering Native Messaging host from {browser}");

                // Remove from Windows registry
                await UnregisterFromRegistryAsync(browser);

                // Remove manifest file
                var manifestPath = GetManifestPath(browser);
                if (File.Exists(manifestPath))
                {
                    File.Delete(manifestPath);
                    _logger.LogDebug($"Deleted manifest file: {manifestPath}");
                }

                _logger.LogInfo($"Successfully unregistered from {browser}");
                OnRegistrationStateChanged(false, browser, "Unregistration successful");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to unregister from {browser}", ex);
                OnRegistrationError(ex, browser, "unregister");
                // Don't throw here to allow other browsers to be unregistered
            }
        }

        private async Task<string> CreateManifestFileAsync(string browser, string executablePath)
        {
            var manifestPath = GetManifestPath(browser);
            
            var extensionIds = _configuration.GetExtensionIds(browser);
            
            object manifest;
            
            if (browser.ToLower() == "firefox")
            {
                manifest = new
                {
                    name = _configuration.HostName,
                    description = "دانلود منجر حامد - Native Messaging Host",
                    path = executablePath,
                    type = "stdio",
                    allowed_extensions = extensionIds
                };
            }
            else
            {
                manifest = new
                {
                    name = _configuration.HostName,
                    description = "دانلود منجر حامد - Native Messaging Host",
                    path = executablePath,
                    type = "stdio",
                    allowed_origins = extensionIds
                };
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var jsonContent = JsonSerializer.Serialize(manifest, jsonOptions);
            
            await File.WriteAllTextAsync(manifestPath, jsonContent);
            
            _logger.LogDebug($"Created manifest file for {browser}: {manifestPath}");
            return manifestPath;
        }

        private async Task RegisterInRegistryAsync(string browser, string manifestPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var registryPath = BrowserRegistryPaths[browser];
                    
                    using var key = Registry.CurrentUser.CreateSubKey($"{registryPath}\\{_configuration.HostName}");
                    if (key == null)
                    {
                        throw new InvalidOperationException($"Failed to create registry key for {browser}");
                    }

                    key.SetValue("", manifestPath, RegistryValueKind.String);
                    
                    _logger.LogDebug($"Registered in registry for {browser}: {registryPath}\\{_configuration.HostName}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException($"Access denied when registering for {browser}. Please run as administrator.", ex);
                }
            });
        }

        private async Task UnregisterFromRegistryAsync(string browser)
        {
            await Task.Run(() =>
            {
                try
                {
                    var registryPath = BrowserRegistryPaths[browser];
                    
                    using var parentKey = Registry.CurrentUser.OpenSubKey(registryPath, true);
                    if (parentKey != null)
                    {
                        parentKey.DeleteSubKey(_configuration.HostName, false);
                        _logger.LogDebug($"Unregistered from registry for {browser}");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException($"Access denied when unregistering from {browser}. Please run as administrator.", ex);
                }
                catch (ArgumentException)
                {
                    // Key doesn't exist, which is fine for unregistration
                    _logger.LogDebug($"Registry key for {browser} was already removed or didn't exist");
                }
            });
        }

        private bool CheckRegistrationStatus()
        {
            try
            {
                foreach (var browser in BrowserRegistryPaths.Keys)
                {
                    var registryPath = BrowserRegistryPaths[browser];
                    
                    using var key = Registry.CurrentUser.OpenSubKey($"{registryPath}\\{_configuration.HostName}");
                    if (key != null)
                    {
                        var manifestPath = key.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
                        {
                            return true; // At least one browser is registered
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error checking registration status", ex);
                return false;
            }
        }

        private string? GetRegisteredHostPath()
        {
            try
            {
                foreach (var browser in BrowserRegistryPaths.Keys)
                {
                    var registryPath = BrowserRegistryPaths[browser];
                    
                    using var key = Registry.CurrentUser.OpenSubKey($"{registryPath}\\{_configuration.HostName}");
                    if (key != null)
                    {
                        var manifestPath = key.GetValue("")?.ToString();
                        if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
                        {
                            // Read the manifest to get the executable path
                            var manifestContent = File.ReadAllText(manifestPath);
                            var manifest = JsonSerializer.Deserialize<JsonElement>(manifestContent);
                            
                            if (manifest.TryGetProperty("path", out var pathElement))
                            {
                                return pathElement.GetString();
                            }
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error getting registered host path", ex);
                return null;
            }
        }

        private string GetManifestPath(string browser)
        {
            return Path.Combine(_manifestsDirectory, $"{_configuration.HostName}.{browser}.json");
        }

        protected virtual void OnRegistrationStateChanged(bool isRegistered, string browser, string message)
        {
            RegistrationStateChanged?.Invoke(this, new RegistrationStateEventArgs(isRegistered, browser, message));
        }

        protected virtual void OnRegistrationError(Exception exception, string browser, string operation)
        {
            RegistrationError?.Invoke(this, new RegistrationErrorEventArgs(exception, browser, operation));
        }

        protected virtual void OnRegistrationError(string message, string browser, string operation)
        {
            RegistrationError?.Invoke(this, new RegistrationErrorEventArgs(message, browser, operation));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            // No unmanaged resources to dispose, but we mark as disposed
            _logger.LogDebug("Native Messaging Registrar disposed");
        }
    }
}