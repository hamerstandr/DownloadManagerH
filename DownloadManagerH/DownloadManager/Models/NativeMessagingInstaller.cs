using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Handles installation and setup of Native Messaging components
    /// </summary>
    public class NativeMessagingInstaller
    {
        private readonly ILogger _logger;
        private readonly NativeMessagingRegistrar _registrar;
        private readonly NativeMessagingConfiguration _configuration;

        public NativeMessagingInstaller(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = new NativeMessagingConfiguration(logger);
            _registrar = new NativeMessagingRegistrar(logger, _configuration);
        }

        /// <summary>
        /// Performs initial setup of Native Messaging (called on first run)
        /// </summary>
        public async Task<bool> SetupAsync()
        {
            try
            {
                _logger.LogInfo("Starting Native Messaging setup");

                // Check if already registered
                if (_registrar.IsRegistered)
                {
                    _logger.LogInfo("Native Messaging already registered");
                    return true;
                }

                // Register the host
                await _registrar.RegisterHostAsync();

                _logger.LogInfo("Native Messaging setup completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to setup Native Messaging", ex);
                return false;
            }
        }

        /// <summary>
        /// Performs cleanup of Native Messaging (called on uninstall)
        /// </summary>
        public async Task<bool> CleanupAsync()
        {
            try
            {
                _logger.LogInfo("Starting Native Messaging cleanup");

                // Unregister the host
                await _registrar.UnregisterHostAsync();

                _logger.LogInfo("Native Messaging cleanup completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to cleanup Native Messaging", ex);
                return false;
            }
        }

        /// <summary>
        /// Updates Native Messaging registration when app is moved
        /// </summary>
        public async Task<bool> UpdateRegistrationAsync()
        {
            try
            {
                _logger.LogInfo("Updating Native Messaging registration");

                var currentPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentPath))
                {
                    _logger.LogError("Cannot determine current executable path");
                    return false;
                }

                var registeredPath = _registrar.RegisteredHostPath;
                
                // Check if path has changed
                if (!string.IsNullOrEmpty(registeredPath) && 
                    string.Equals(registeredPath, currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInfo("Native Messaging registration is up to date");
                    return true;
                }

                // Update registration with new path
                await _registrar.UpdateHostPathAsync(currentPath);

                _logger.LogInfo($"Updated Native Messaging registration to: {currentPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update Native Messaging registration", ex);
                return false;
            }
        }

        /// <summary>
        /// Checks if Native Messaging is properly configured
        /// </summary>
        public bool IsProperlyConfigured()
        {
            try
            {
                // Check if registered
                if (!_registrar.IsRegistered)
                {
                    _logger.LogWarning("Native Messaging is not registered");
                    return false;
                }

                // Check if executable path is correct
                var registeredPath = _registrar.RegisteredHostPath;
                var currentPath = Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrEmpty(registeredPath) || string.IsNullOrEmpty(currentPath))
                {
                    _logger.LogWarning("Cannot verify Native Messaging paths");
                    return false;
                }

                if (!string.Equals(registeredPath, currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"Native Messaging path mismatch. Registered: {registeredPath}, Current: {currentPath}");
                    return false;
                }

                // Check if executable exists
                if (!File.Exists(registeredPath))
                {
                    _logger.LogWarning($"Registered executable does not exist: {registeredPath}");
                    return false;
                }

                _logger.LogDebug("Native Messaging is properly configured");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error checking Native Messaging configuration", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets diagnostic information about Native Messaging setup
        /// </summary>
        public NativeMessagingDiagnostics GetDiagnostics()
        {
            var diagnostics = new NativeMessagingDiagnostics();

            try
            {
                diagnostics.IsRegistered = _registrar.IsRegistered;
                diagnostics.RegisteredPath = _registrar.RegisteredHostPath;
                diagnostics.CurrentPath = Process.GetCurrentProcess().MainModule?.FileName;
                diagnostics.IsEnabled = _configuration.IsEnabled;
                diagnostics.IsDownloadInterceptionEnabled = _configuration.IsDownloadInterceptionEnabled;
                diagnostics.HostName = _configuration.HostName;

                // Check extension IDs
                diagnostics.ChromeExtensionIds = _configuration.GetExtensionIds("chrome");
                diagnostics.EdgeExtensionIds = _configuration.GetExtensionIds("edge");
                diagnostics.FirefoxExtensionIds = _configuration.GetExtensionIds("firefox");

                diagnostics.IsProperlyConfigured = IsProperlyConfigured();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting Native Messaging diagnostics", ex);
                diagnostics.ErrorMessage = ex.Message;
            }

            return diagnostics;
        }

        /// <summary>
        /// Updates extension IDs for a specific browser
        /// </summary>
        public void UpdateExtensionIds(string browser, string extensionId)
        {
            try
            {
                var extensionIds = new List<string> { extensionId };
                _configuration.UpdateExtensionIds(browser, extensionIds);
                
                _logger.LogInfo($"Updated {browser} extension ID: {extensionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update {browser} extension ID", ex);
                throw;
            }
        }
    }

    /// <summary>
    /// Diagnostic information about Native Messaging setup
    /// </summary>
    public class NativeMessagingDiagnostics
    {
        public bool IsRegistered { get; set; }
        public string? RegisteredPath { get; set; }
        public string? CurrentPath { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsDownloadInterceptionEnabled { get; set; }
        public string HostName { get; set; } = "";
        public List<string> ChromeExtensionIds { get; set; } = new();
        public List<string> EdgeExtensionIds { get; set; } = new();
        public List<string> FirefoxExtensionIds { get; set; } = new();
        public bool IsProperlyConfigured { get; set; }
        public string? ErrorMessage { get; set; }

        public override string ToString()
        {
            return $"Native Messaging Diagnostics:\n" +
                   $"  Registered: {IsRegistered}\n" +
                   $"  Enabled: {IsEnabled}\n" +
                   $"  Download Interception: {IsDownloadInterceptionEnabled}\n" +
                   $"  Host Name: {HostName}\n" +
                   $"  Registered Path: {RegisteredPath ?? "N/A"}\n" +
                   $"  Current Path: {CurrentPath ?? "N/A"}\n" +
                   $"  Properly Configured: {IsProperlyConfigured}\n" +
                   $"  Chrome Extensions: {string.Join(", ", ChromeExtensionIds)}\n" +
                   $"  Edge Extensions: {string.Join(", ", EdgeExtensionIds)}\n" +
                   $"  Firefox Extensions: {string.Join(", ", FirefoxExtensionIds)}\n" +
                   $"  Error: {ErrorMessage ?? "None"}";
        }
    }
}