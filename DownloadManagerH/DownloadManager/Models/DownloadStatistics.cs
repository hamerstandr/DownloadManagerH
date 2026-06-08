using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadManagerH.Models
{
    /// <summary>
    /// آمار دانلود
    /// </summary>
    public class DownloadStatistics
    {
        /// <summary>
        /// زمان شروع دانلود
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// زمان پایان دانلود
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// کل زمان دانلود
        /// </summary>
        public TimeSpan TotalDownloadTime 
        { 
            get 
            { 
                if (EndTime.HasValue)
                    return EndTime.Value - StartTime;
                return DateTime.Now - StartTime;
            } 
        }

        /// <summary>
        /// میانگین سرعت (بایت در ثانیه)
        /// </summary>
        public double AverageSpeed { get; set; }

        /// <summary>
        /// حداکثر سرعت (بایت در ثانیه)
        /// </summary>
        public double PeakSpeed { get; set; }

        /// <summary>
        /// تعداد تلاش‌های مجدد
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// تاریخچه سرعت
        /// </summary>
        public List<SpeedSample> SpeedHistory { get; set; } = new();

        /// <summary>
        /// تعداد وقفه‌ها
        /// </summary>
        public int PauseCount { get; set; }

        /// <summary>
        /// کل زمان وقفه
        /// </summary>
        public TimeSpan TotalPauseTime { get; set; }

        /// <summary>
        /// تعداد خطاها
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// آخرین خطا
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// تاریخ آخرین خطا
        /// </summary>
        public DateTime? LastErrorTime { get; set; }

        /// <summary>
        /// افزودن نمونه سرعت
        /// </summary>
        /// <param name="speed">سرعت (بایت در ثانیه)</param>
        public void AddSpeedSample(double speed)
        {
            var sample = new SpeedSample
            {
                Timestamp = DateTime.Now,
                Speed = speed
            };

            SpeedHistory.Add(sample);

            // نگهداری حداکثر 100 نمونه
            if (SpeedHistory.Count > 100)
            {
                SpeedHistory.RemoveAt(0);
            }

            // به‌روزرسانی حداکثر سرعت
            if (speed > PeakSpeed)
            {
                PeakSpeed = speed;
            }

            // محاسبه میانگین سرعت
            if (SpeedHistory.Count > 0)
            {
                AverageSpeed = SpeedHistory.Average(s => s.Speed);
            }
        }

        /// <summary>
        /// ثبت خطا
        /// </summary>
        /// <param name="error">پیام خطا</param>
        public void RecordError(string error)
        {
            ErrorCount++;
            LastError = error;
            LastErrorTime = DateTime.Now;
        }

        /// <summary>
        /// ثبت وقفه
        /// </summary>
        public void RecordPause()
        {
            PauseCount++;
        }

        /// <summary>
        /// ثبت تلاش مجدد
        /// </summary>
        public void RecordRetry()
        {
            RetryCount++;
        }

        /// <summary>
        /// تکمیل دانلود
        /// </summary>
        public void Complete()
        {
            EndTime = DateTime.Now;
        }

        /// <summary>
        /// دریافت سرعت فعلی
        /// </summary>
        /// <returns>سرعت فعلی (بایت در ثانیه)</returns>
        public double GetCurrentSpeed()
        {
            if (SpeedHistory.Count == 0)
                return 0;

            // میانگین 5 نمونه آخر
            var recentSamples = SpeedHistory.TakeLast(5);
            return recentSamples.Average(s => s.Speed);
        }

        /// <summary>
        /// دریافت سرعت به صورت متنی
        /// </summary>
        /// <returns>سرعت به صورت قابل خواندن</returns>
        public string GetCurrentSpeedText()
        {
            var speed = GetCurrentSpeed();
            return FormatSpeed(speed);
        }

        /// <summary>
        /// دریافت میانگین سرعت به صورت متنی
        /// </summary>
        /// <returns>میانگین سرعت به صورت قابل خواندن</returns>
        public string GetAverageSpeedText()
        {
            return FormatSpeed(AverageSpeed);
        }

        /// <summary>
        /// دریافت حداکثر سرعت به صورت متنی
        /// </summary>
        /// <returns>حداکثر سرعت به صورت قابل خواندن</returns>
        public string GetPeakSpeedText()
        {
            return FormatSpeed(PeakSpeed);
        }

        /// <summary>
        /// فرمت کردن سرعت
        /// </summary>
        /// <param name="bytesPerSecond">سرعت بر حسب بایت در ثانیه</param>
        /// <returns>سرعت فرمت شده</returns>
        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond == 0)
                return "0 B/s";

            string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
            int unitIndex = 0;
            double speed = bytesPerSecond;

            while (speed >= 1024 && unitIndex < units.Length - 1)
            {
                speed /= 1024;
                unitIndex++;
            }

            return $"{speed:F2} {units[unitIndex]}";
        }

        /// <summary>
        /// دریافت زمان باقی‌مانده تخمینی
        /// </summary>
        /// <param name="remainingBytes">بایت‌های باقی‌مانده</param>
        /// <returns>زمان باقی‌مانده</returns>
        public TimeSpan? GetEstimatedTimeRemaining(long remainingBytes)
        {
            var currentSpeed = GetCurrentSpeed();
            
            if (currentSpeed <= 0 || remainingBytes <= 0)
                return null;

            var secondsRemaining = remainingBytes / currentSpeed;
            return TimeSpan.FromSeconds(secondsRemaining);
        }

        /// <summary>
        /// دریافت زمان باقی‌مانده به صورت متنی
        /// </summary>
        /// <param name="remainingBytes">بایت‌های باقی‌مانده</param>
        /// <returns>زمان باقی‌مانده به صورت قابل خواندن</returns>
        public string GetEstimatedTimeRemainingText(long remainingBytes)
        {
            var timeRemaining = GetEstimatedTimeRemaining(remainingBytes);
            
            if (!timeRemaining.HasValue)
                return "نامشخص";

            var time = timeRemaining.Value;
            
            if (time.TotalDays >= 1)
                return $"{time.Days} روز {time.Hours} ساعت";
            
            if (time.TotalHours >= 1)
                return $"{time.Hours} ساعت {time.Minutes} دقیقه";
            
            if (time.TotalMinutes >= 1)
                return $"{time.Minutes} دقیقه {time.Seconds} ثانیه";
            
            return $"{time.Seconds} ثانیه";
        }
    }

    /// <summary>
    /// نمونه سرعت
    /// </summary>
    public class SpeedSample
    {
        /// <summary>
        /// زمان ثبت نمونه
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// سرعت (بایت در ثانیه)
        /// </summary>
        public double Speed { get; set; }
    }
}