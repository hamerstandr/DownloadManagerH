using System;

namespace DownloadManagerH.Models
{
    public enum PartStatus
    {
        Pending,
        Downloading,
        Completed,
        Failed
    }

    public class DownloadPart
    {
        public int Index { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public long Downloaded { get; set; }
        public PartStatus Status { get; set; }
        public string TempFilePath { get; set; } = ""; // مسیر فایل موقت برای هر بخش
        public double Progress => (End > Start) ? (Downloaded * 100.0 / (End - Start + 1)) : 0;
    }
} 