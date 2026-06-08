using System;
using System.Collections.Generic;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// متادیتای فایل دانلود
    /// </summary>
    public class DownloadMetadata
    {
        /// <summary>
        /// نوع محتوا (MIME Type)
        /// </summary>
        public string ContentType { get; set; } = "";

        /// <summary>
        /// طول محتوا
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// ETag برای کنترل کش
        /// </summary>
        public string ETag { get; set; } = "";

        /// <summary>
        /// تاریخ آخرین تغییر
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// اطلاعات سرور
        /// </summary>
        public string ServerInfo { get; set; } = "";

        /// <summary>
        /// هدرهای سفارشی
        /// </summary>
        public Dictionary<string, string> CustomHeaders { get; set; } = new();

        /// <summary>
        /// نام فایل پیشنهادی از سرور
        /// </summary>
        public string SuggestedFileName { get; set; } = "";

        /// <summary>
        /// آیا سرور از Resume پشتیبانی می‌کند
        /// </summary>
        public bool SupportsResume { get; set; }

        /// <summary>
        /// آیا سرور از Range Request پشتیبانی می‌کند
        /// </summary>
        public bool SupportsRangeRequests { get; set; }

        /// <summary>
        /// کدگذاری محتوا
        /// </summary>
        public string ContentEncoding { get; set; } = "";

        /// <summary>
        /// زبان محتوا
        /// </summary>
        public string ContentLanguage { get; set; } = "";

        /// <summary>
        /// تاریخ انقضا
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// تاریخ ایجاد متادیتا
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// بررسی معتبر بودن متادیتا
        /// </summary>
        /// <returns>true اگر معتبر باشد</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ContentType) && ContentLength > 0;
        }

        /// <summary>
        /// دریافت پسوند فایل بر اساس نوع محتوا
        /// </summary>
        /// <returns>پسوند فایل</returns>
        public string GetFileExtension()
        {
            if (string.IsNullOrWhiteSpace(ContentType))
                return ".bin";

            return ContentType.ToLower() switch
            {
                "text/html" => ".html",
                "text/plain" => ".txt",
                "text/css" => ".css",
                "text/javascript" => ".js",
                "application/json" => ".json",
                "application/xml" => ".xml",
                "application/pdf" => ".pdf",
                "application/zip" => ".zip",
                "application/x-rar-compressed" => ".rar",
                "application/x-7z-compressed" => ".7z",
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                "audio/mpeg" => ".mp3",
                "audio/wav" => ".wav",
                "audio/ogg" => ".ogg",
                "video/mp4" => ".mp4",
                "video/avi" => ".avi",
                "video/mkv" => ".mkv",
                "video/webm" => ".webm",
                "application/msword" => ".doc",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                "application/vnd.ms-excel" => ".xls",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
                "application/vnd.ms-powerpoint" => ".ppt",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
                _ => ".bin"
            };
        }

        /// <summary>
        /// تشخیص نوع فایل
        /// </summary>
        /// <returns>نوع فایل</returns>
        public FileType GetFileType()
        {
            if (string.IsNullOrWhiteSpace(ContentType))
                return FileType.Unknown;

            var contentType = ContentType.ToLower();

            if (contentType.StartsWith("image/"))
                return FileType.Image;
            
            if (contentType.StartsWith("video/"))
                return FileType.Video;
            
            if (contentType.StartsWith("audio/"))
                return FileType.Audio;
            
            if (contentType.StartsWith("text/"))
                return FileType.Text;
            
            if (contentType.Contains("zip") || contentType.Contains("rar") || contentType.Contains("7z"))
                return FileType.Archive;
            
            if (contentType.Contains("pdf"))
                return FileType.Document;
            
            if (contentType.Contains("word") || contentType.Contains("excel") || contentType.Contains("powerpoint"))
                return FileType.Document;

            return FileType.Binary;
        }
    }

    /// <summary>
    /// انواع فایل
    /// </summary>
    public enum FileType
    {
        Unknown,
        Image,
        Video,
        Audio,
        Text,
        Document,
        Archive,
        Binary
    }
}