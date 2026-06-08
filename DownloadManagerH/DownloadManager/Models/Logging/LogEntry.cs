using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// کلاس نمایانگر یک ورودی لاگ
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// زمان ثبت لاگ
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// سطح لاگ
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// متن پیام اصلی
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// دسته‌بندی لاگ (مثل Download، UI، Plugin)
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// اطلاعات زمینه‌ای به صورت JSON
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// جزئیات استثنا در صورت وجود
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// خصوصیات اضافی به صورت دیکشنری
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// سازنده پیش‌فرض
        /// </summary>
        public LogEntry()
        {
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// سازنده با پارامترهای اصلی
        /// </summary>
        /// <param name="level">سطح لاگ</param>
        /// <param name="message">متن پیام</param>
        /// <param name="category">دسته‌بندی</param>
        public LogEntry(LogLevel level, string message, string category = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            Category = category ?? "General";
        }

        /// <summary>
        /// تبدیل اطلاعات زمینه‌ای به JSON
        /// </summary>
        /// <param name="context">اطلاعات زمینه‌ای</param>
        public void SetContext(object context)
        {
            if (context != null)
            {
                try
                {
                    Context = JsonSerializer.Serialize(context, new JsonSerializerOptions 
                    { 
                        WriteIndented = false,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                }
                catch
                {
                    Context = context.ToString();
                }
            }
        }

        /// <summary>
        /// افزودن خصوصیت به لاگ
        /// </summary>
        /// <param name="key">کلید</param>
        /// <param name="value">مقدار</param>
        public void AddProperty(string key, object value)
        {
            Properties[key] = value;
        }

        /// <summary>
        /// نمایش متنی لاگ
        /// </summary>
        /// <returns>رشته نمایشی</returns>
        public override string ToString()
        {
            var levelText = Level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                _ => Level.ToString()
            };

            var result = $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{levelText}] [{Category}] {Message}";
            
            if (!string.IsNullOrEmpty(Context))
            {
                result += $" | Context: {Context}";
            }

            if (Exception != null)
            {
                result += $" | Exception: {Exception.Message}";
            }

            return result;
        }
    }
}