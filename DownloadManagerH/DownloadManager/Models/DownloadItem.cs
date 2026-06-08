using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DownloadManagerH.Models
{
    public enum DownloadStatus
    {
        Pending,
        Downloading,
        Paused,
        Completed,
        Failed,
        Stopped
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string _url="";
        public string Url { get => _url; set { _url = value; OnPropertyChanged(); } }
        private string _fileName="";
        public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(); } }
        private string _savePath = "";
        public string SavePath { get => _savePath; set { _savePath = value; OnPropertyChanged(); } }
        private DownloadStatus _status;
        public DownloadStatus Status { get => _status; set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusFa)); } }
        private double _progress;
        public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
        private long _totalBytes;
        public long TotalBytes { get => _totalBytes; set { _totalBytes = value; OnPropertyChanged(); } }
        private long _downloadedBytes;
        public long DownloadedBytes { get => _downloadedBytes; set { _downloadedBytes = value; OnPropertyChanged(); } }
        private DateTime? _scheduledTime;
        public DateTime? ScheduledTime { get => _scheduledTime; set { _scheduledTime = value; OnPropertyChanged(); } }
        private string _group = "";
        public string Group { get => _group; set { _group = value; OnPropertyChanged(); } }
        private bool _canResume = true;
        public bool CanResume { get => _canResume; set { _canResume = value; OnPropertyChanged(); } }
        public int ParallelParts = 1;
        private List<DownloadPart> _parts = [];
        public List<DownloadPart> Parts { get => _parts; set { _parts = value; OnPropertyChanged(); } }
        private string _speed = "";
        public string Speed { get => _speed; set { _speed = value; OnPropertyChanged(); } }
        
        // خصوصیات جدید برای بهبود عملکرد
        private int _retryCount = 0;
        public int RetryCount { get => _retryCount; set { _retryCount = value; OnPropertyChanged(); } }
        
        private DateTime? _lastRetryTime;
        public DateTime? LastRetryTime { get => _lastRetryTime; set { _lastRetryTime = value; OnPropertyChanged(); } }
        
        private DownloadPriority _priority = DownloadPriority.Normal;
        public DownloadPriority Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }
        
        private Dictionary<string, string> _headers = new();
        public Dictionary<string, string> Headers { get => _headers; set { _headers = value; OnPropertyChanged(); } }
        
        private string _referrer = "";
        public string Referrer { get => _referrer; set { _referrer = value; OnPropertyChanged(); } }
        
        private List<string> _cookies = [];
        public List<string> Cookies { get => _cookies; set { _cookies = value; OnPropertyChanged(); } }
        
        private DownloadMetadata? _metadata;
        public DownloadMetadata? Metadata { get => _metadata; set { _metadata = value; OnPropertyChanged(); } }
        
        private List<PostDownloadAction> _postActions = [];
        public List<PostDownloadAction> PostActions { get => _postActions; set { _postActions = value; OnPropertyChanged(); } }
        
        private DownloadStatistics? _statistics;
        public DownloadStatistics? Statistics { get => _statistics; set { _statistics = value; OnPropertyChanged(); } }
        
        public string StatusFa
        {
            get
            {
                return Status switch
                {
                    DownloadStatus.Pending => "در انتظار",
                    DownloadStatus.Downloading => "در حال دانلود",
                    DownloadStatus.Paused => "متوقف موقت",
                    DownloadStatus.Completed => "تکمیل شده",
                    DownloadStatus.Failed => "ناموفق",
                    DownloadStatus.Stopped => "متوقف شده",
                    _ => Status.ToString()
                };
            }
        }
        
        /// <summary>
        /// آیا دانلود قابل تلاش مجدد است
        /// </summary>
        public bool CanRetry => Status == DownloadStatus.Failed && RetryCount < 3;
        
        /// <summary>
        /// درصد پیشرفت به صورت متنی
        /// </summary>
        public string ProgressText => $"{Progress:F1}%";
        
        /// <summary>
        /// اندازه فایل به صورت قابل خواندن
        /// </summary>
        public string FileSizeText => FormatFileSize(TotalBytes);
        
        /// <summary>
        /// مقدار دانلود شده به صورت قابل خواندن
        /// </summary>
        public string DownloadedSizeText => FormatFileSize(DownloadedBytes);
        
        /// <summary>
        /// فرمت کردن اندازه فایل
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:F2} {sizes[order]}";
        }
    }
} 