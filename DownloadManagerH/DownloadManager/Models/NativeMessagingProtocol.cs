using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Native Messaging Protocol handler with validation and serialization utilities
    /// </summary>
    public static class NativeMessagingProtocol
    {
        /// <summary>
        /// Browser priority levels for message processing
        /// </summary>
        public enum BrowserPriority
        {
            Low = 1,
            Normal = 2,
            High = 3,
            Critical = 4
        }

        /// <summary>
        /// Browser priority mapping
        /// </summary>
        public static readonly Dictionary<string, BrowserPriority> BrowserPriorityMap = new()
        {
            { "edge", BrowserPriority.Critical },      // Edge has highest priority
            { "chrome", BrowserPriority.High },       // Chrome has high priority
            { "firefox", BrowserPriority.Normal },    // Firefox has normal priority
            { "unknown", BrowserPriority.Low }        // Unknown browsers have low priority
        };

        /// <summary>
        /// Supported message types
        /// </summary>
        public static readonly HashSet<string> SupportedMessageTypes = new()
        {
            "addDownload",
            "getStatus",
            "getSettings",
            "focus",
            "interceptDownload",
            "response",
            "statusResponse",
            "settingsResponse"
        };

        /// <summary>
        /// Maximum message size (1MB)
        /// </summary>
        public const int MaxMessageSize = 1024 * 1024;

        /// <summary>
        /// Maximum number of links per batch download
        /// </summary>
        public const int MaxLinksPerBatch = 100;

        /// <summary>
        /// JSON serializer options for Native Messaging
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Validates a Native Message
        /// </summary>
        public static ValidationResult ValidateMessage(NativeMessage message)
        {
            var result = new ValidationResult();

            if (message == null)
            {
                result.AddError("Message cannot be null");
                return result;
            }

            // Validate message type
            if (string.IsNullOrWhiteSpace(message.Type))
            {
                result.AddError("Message type is required");
            }
            else if (!SupportedMessageTypes.Contains(message.Type))
            {
                result.AddError($"Unsupported message type: {message.Type}");
            }

            // Validate message ID
            if (string.IsNullOrWhiteSpace(message.Id))
            {
                result.AddError("Message ID is required");
            }

            // Validate timestamp
            if (message.Timestamp <= 0)
            {
                result.AddError("Valid timestamp is required");
            }

            // Validate browser
            if (string.IsNullOrWhiteSpace(message.Browser))
            {
                message.Browser = "unknown";
            }

            // Validate priority
            if (string.IsNullOrWhiteSpace(message.Priority))
            {
                message.Priority = "normal";
            }

            return result;
        }

        /// <summary>
        /// Validates an AddDownloadMessage
        /// </summary>
        public static ValidationResult ValidateAddDownloadMessage(AddDownloadMessage message)
        {
            var result = ValidateMessage(message);

            if (message.Data == null)
            {
                result.AddError("Download data is required");
                return result;
            }

            // Validate links
            if (message.Data.Links == null || message.Data.Links.Count == 0)
            {
                result.AddError("At least one download link is required");
            }
            else if (message.Data.Links.Count > MaxLinksPerBatch)
            {
                result.AddError($"Too many links: {message.Data.Links.Count} (max: {MaxLinksPerBatch})");
            }
            else
            {
                for (int i = 0; i < message.Data.Links.Count; i++)
                {
                    var linkResult = ValidateDownloadLink(message.Data.Links[i], i);
                    result.Merge(linkResult);
                }
            }

            // Validate request type
            var validRequestTypes = new[] { "single", "batch", "media" };
            if (!string.IsNullOrWhiteSpace(message.Data.RequestType) && 
                !validRequestTypes.Contains(message.Data.RequestType))
            {
                result.AddError($"Invalid request type: {message.Data.RequestType}");
            }

            return result;
        }

        /// <summary>
        /// Validates a download link
        /// </summary>
        public static ValidationResult ValidateDownloadLink(DownloadLinkData link, int index = -1)
        {
            var result = new ValidationResult();
            var prefix = index >= 0 ? $"Link {index}: " : "";

            if (link == null)
            {
                result.AddError($"{prefix}Link cannot be null");
                return result;
            }

            // Validate URL
            if (string.IsNullOrWhiteSpace(link.Url))
            {
                result.AddError($"{prefix}URL is required");
            }
            else if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
            {
                result.AddError($"{prefix}Invalid URL format: {link.Url}");
            }
            else if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "ftp")
            {
                result.AddError($"{prefix}Unsupported URL scheme: {uri.Scheme}");
            }

            // Validate file size if provided
            if (link.TotalBytes.HasValue && link.TotalBytes.Value < 0)
            {
                result.AddError($"{prefix}Invalid file size: {link.TotalBytes.Value}");
            }

            // Validate filename if provided
            if (!string.IsNullOrWhiteSpace(link.Filename))
            {
                var invalidChars = System.IO.Path.GetInvalidFileNameChars();
                if (link.Filename.Any(c => invalidChars.Contains(c)))
                {
                    result.AddError($"{prefix}Filename contains invalid characters: {link.Filename}");
                }
            }

            return result;
        }

        /// <summary>
        /// Validates an InterceptDownloadMessage
        /// </summary>
        public static ValidationResult ValidateInterceptDownloadMessage(InterceptDownloadMessage message)
        {
            var result = ValidateMessage(message);

            if (message.Data == null)
            {
                result.AddError("Intercept download data is required");
                return result;
            }

            // Validate download ID
            if (string.IsNullOrWhiteSpace(message.Data.DownloadId))
            {
                result.AddError("Download ID is required for interception");
            }

            // Validate URL
            if (string.IsNullOrWhiteSpace(message.Data.Url))
            {
                result.AddError("URL is required for interception");
            }
            else if (!Uri.TryCreate(message.Data.Url, UriKind.Absolute, out var uri))
            {
                result.AddError($"Invalid URL format: {message.Data.Url}");
            }

            return result;
        }

        /// <summary>
        /// Serializes a message to JSON
        /// </summary>
        public static string SerializeMessage(object message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                return JsonSerializer.Serialize(message, JsonOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to serialize message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes a JSON message to the specified type
        /// </summary>
        public static T? DeserializeMessage<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or empty", nameof(json));

            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to deserialize message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes a JSON message to NativeMessage to determine type
        /// </summary>
        public static NativeMessage? DeserializeBaseMessage(string json)
        {
            return DeserializeMessage<NativeMessage>(json);
        }

        /// <summary>
        /// Gets browser priority for message processing
        /// </summary>
        public static BrowserPriority GetBrowserPriority(string browser)
        {
            if (string.IsNullOrWhiteSpace(browser))
                return BrowserPriority.Low;

            return BrowserPriorityMap.TryGetValue(browser.ToLower(), out var priority) 
                ? priority 
                : BrowserPriority.Low;
        }

        /// <summary>
        /// Determines if download interception is supported for the browser
        /// </summary>
        public static bool SupportsDownloadInterception(string browser)
        {
            if (string.IsNullOrWhiteSpace(browser))
                return false;

            return browser.ToLower() switch
            {
                "edge" => true,      // Full support
                "chrome" => true,    // Full support
                "firefox" => false,  // Limited support (not implemented yet)
                _ => false
            };
        }

        /// <summary>
        /// Creates a standardized error response
        /// </summary>
        public static ResponseMessage CreateErrorResponse(string errorMessage, string errorType, string? messageId = null, string? browser = null)
        {
            return new ResponseMessage
            {
                Id = messageId ?? Guid.NewGuid().ToString(),
                Browser = browser ?? "unknown",
                Data = new ResponseData
                {
                    Success = false,
                    Message = errorMessage,
                    Errors = new List<string> { errorMessage }
                }
            };
        }

        /// <summary>
        /// Creates a standardized success response
        /// </summary>
        public static ResponseMessage CreateSuccessResponse(string message, int addedCount = 0, string? messageId = null, string? browser = null)
        {
            return new ResponseMessage
            {
                Id = messageId ?? Guid.NewGuid().ToString(),
                Browser = browser ?? "unknown",
                Data = new ResponseData
                {
                    Success = true,
                    Message = message,
                    AddedCount = addedCount
                }
            };
        }
    }

    /// <summary>
    /// Validation result container
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<string> Errors { get; } = new();

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
            }
        }

        public void Merge(ValidationResult other)
        {
            if (other != null)
            {
                Errors.AddRange(other.Errors);
            }
        }

        public override string ToString()
        {
            return IsValid ? "Valid" : string.Join("; ", Errors);
        }
    }
}