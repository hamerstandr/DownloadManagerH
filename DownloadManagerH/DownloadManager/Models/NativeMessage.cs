using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// Base class for all Native Messaging protocol messages
    /// </summary>
    public class NativeMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        [JsonPropertyName("browser")]
        public string Browser { get; set; } = "unknown";

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "normal";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        /// <summary>
        /// Gets the browser priority for message processing
        /// </summary>
        public NativeMessagingProtocol.BrowserPriority GetBrowserPriority()
        {
            return NativeMessagingProtocol.GetBrowserPriority(Browser);
        }

        /// <summary>
        /// Validates the message using the protocol validation rules
        /// </summary>
        public ValidationResult Validate()
        {
            return NativeMessagingProtocol.ValidateMessage(this);
        }
    }

    /// <summary>
    /// Message for adding downloads to the application
    /// </summary>
    public class AddDownloadMessage : NativeMessage
    {
        public AddDownloadMessage()
        {
            Type = "addDownload";
        }

        [JsonPropertyName("data")]
        public new AddDownloadData? Data { get; set; }

        /// <summary>
        /// Validates the add download message
        /// </summary>
        public new ValidationResult Validate()
        {
            return NativeMessagingProtocol.ValidateAddDownloadMessage(this);
        }
    }

    /// <summary>
    /// Data structure for add download requests
    /// </summary>
    public class AddDownloadData
    {
        [JsonPropertyName("links")]
        public List<DownloadLinkData> Links { get; set; } = new();

        [JsonPropertyName("requestType")]
        public string RequestType { get; set; } = "single"; // single, batch, media

        [JsonPropertyName("group")]
        public string? Group { get; set; }

        [JsonPropertyName("savePath")]
        public string? SavePath { get; set; }
    }

    /// <summary>
    /// Individual download link data
    /// </summary>
    public class DownloadLinkData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("referrer")]
        public string? Referrer { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string>? Headers { get; set; }

        [JsonPropertyName("cookies")]
        public string? Cookies { get; set; }

        [JsonPropertyName("intercepted")]
        public bool Intercepted { get; set; } = false;

        [JsonPropertyName("totalBytes")]
        public long? TotalBytes { get; set; }

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }
    }

    /// <summary>
    /// Message for requesting application status
    /// </summary>
    public class StatusRequestMessage : NativeMessage
    {
        public StatusRequestMessage()
        {
            Type = "getStatus";
        }
    }

    /// <summary>
    /// Response message with application status
    /// </summary>
    public class StatusResponseMessage : NativeMessage
    {
        public StatusResponseMessage()
        {
            Type = "statusResponse";
        }

        [JsonPropertyName("data")]
        public new StatusData? Data { get; set; }
    }

    /// <summary>
    /// Status data structure
    /// </summary>
    public class StatusData
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "running";

        [JsonPropertyName("activeDownloads")]
        public int ActiveDownloads { get; set; }

        [JsonPropertyName("totalDownloads")]
        public int TotalDownloads { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("uptime")]
        public TimeSpan Uptime { get; set; }
    }

    /// <summary>
    /// Message for download interception (redirecting browser downloads to app)
    /// </summary>
    public class InterceptDownloadMessage : NativeMessage
    {
        public InterceptDownloadMessage()
        {
            Type = "interceptDownload";
        }

        [JsonPropertyName("data")]
        public new InterceptDownloadData? Data { get; set; }
    }

    /// <summary>
    /// Data for download interception
    /// </summary>
    public class InterceptDownloadData
    {
        [JsonPropertyName("downloadId")]
        public string DownloadId { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("totalBytes")]
        public long? TotalBytes { get; set; }

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }

        [JsonPropertyName("referrer")]
        public string? Referrer { get; set; }
    }

    /// <summary>
    /// Generic response message
    /// </summary>
    public class ResponseMessage : NativeMessage
    {
        public ResponseMessage()
        {
            Type = "response";
        }

        [JsonPropertyName("data")]
        public new ResponseData? Data { get; set; }
    }

    /// <summary>
    /// Response data structure
    /// </summary>
    public class ResponseData
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("addedCount")]
        public int AddedCount { get; set; }

        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; set; }

        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new();

        [JsonPropertyName("addedUrls")]
        public List<string> AddedUrls { get; set; } = new();
    }

    /// <summary>
    /// Message for focusing the main application window
    /// </summary>
    public class FocusMessage : NativeMessage
    {
        public FocusMessage()
        {
            Type = "focus";
        }
    }

    /// <summary>
    /// Message for requesting application settings
    /// </summary>
    public class SettingsRequestMessage : NativeMessage
    {
        public SettingsRequestMessage()
        {
            Type = "getSettings";
        }
    }

    /// <summary>
    /// Response with application settings
    /// </summary>
    public class SettingsResponseMessage : NativeMessage
    {
        public SettingsResponseMessage()
        {
            Type = "settingsResponse";
        }

        [JsonPropertyName("data")]
        public new DownloadManagerH.Models.SettingsData? Data { get; set; }
    }
}