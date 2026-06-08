using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DownloadManagerH.Models.Logging;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Configuration manager for Native Messaging extension IDs and settings
    /// </summary>
    public class NativeMessagingConfiguration
    {
        private readonly ILogger _logger;
        private readonly string _configPath;
        private NativeMessagingConfig _config;

        public NativeMessagingConfiguration(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDir = Path.Combine(appDataPath, "DownloadManagerH");
            Directory.CreateDirectory(configDir);
            
            _configPath = Path.Combine(configDir, "native-messaging-config.json");
            _config = LoadConfiguration();
        }

        /// <summary>
        /// Gets the extension IDs for a specific browser
        /// </summary>
        public List<string> GetExtensionIds(string browser)
        {
            return browser.ToLower() switch
            {
                "chrome" => _config.ChromeExtensionIds,
                "edge" => _config.EdgeExtensionIds,
                "firefox" => _config.FirefoxExtensionIds,
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Updates extension IDs for a specific browser
        /// </summary>
        public void UpdateExtensionIds(string browser, List<string> extensionIds)
        {
            switch (browser.ToLower())
            {
                case "chrome":
                    _config.ChromeExtensionIds = extensionIds;
                    break;
                case "edge":
                    _config.EdgeExtensionIds = extensionIds;
                    break;
                case "firefox":
                    _config.FirefoxExtensionIds = extensionIds;
                    break;
                default:
                    throw new ArgumentException($"Unknown browser: {browser}");
            }

            SaveConfiguration();
            _logger.LogInfo($"Updated extension IDs for {browser}: {string.Join(", ", extensionIds)}");
        }

        /// <summary>
        /// Gets the Native Messaging host name
        /// </summary>
        public string HostName => _config.HostName;

        /// <summary>
        /// Gets whether Native Messaging is enabled
        /// </summary>
        public bool IsEnabled => _config.IsEnabled;

        /// <summary>
        /// Sets whether Native Messaging is enabled
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _config.IsEnabled = enabled;
            SaveConfiguration();
            _logger.LogInfo($"Native Messaging enabled: {enabled}");
        }

        /// <summary>
        /// Gets whether download interception is enabled
        /// </summary>
        public bool IsDownloadInterceptionEnabled => _config.EnableDownloadInterception;

        /// <summary>
        /// Sets whether download interception is enabled
        /// </summary>
        public void SetDownloadInterceptionEnabled(bool enabled)
        {
            _config.EnableDownloadInterception = enabled;
            SaveConfiguration();
            _logger.LogInfo($"Download interception enabled: {enabled}");
        }

        private NativeMessagingConfig LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<NativeMessagingConfig>(json);
                    if (config != null)
                    {
                        _logger.LogInfo("Loaded Native Messaging configuration");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load Native Messaging configuration, using defaults", ex);
            }

            // Return default configuration
            return new NativeMessagingConfig();
        }

        private void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(_configPath, json);
                _logger.LogDebug("Saved Native Messaging configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save Native Messaging configuration", ex);
            }
        }
    }

    /// <summary>
    /// Configuration data structure for Native Messaging
    /// </summary>
    public class NativeMessagingConfig
    {
        public string HostName { get; set; } = "com.hameddownloadmanager.host";
        public bool IsEnabled { get; set; } = true;
        public bool EnableDownloadInterception { get; set; } = true;
        
        public List<string> ChromeExtensionIds { get; set; } = new()
        {
            "chrome-extension://placeholder-chrome-extension-id/"
        };
        
        public List<string> EdgeExtensionIds { get; set; } = new()
        {
            "extension://placeholder-edge-extension-id/"
        };
        
        public List<string> FirefoxExtensionIds { get; set; } = new()
        {
            "placeholder-firefox-extension-id@mozilla.org"
        };
    }
}