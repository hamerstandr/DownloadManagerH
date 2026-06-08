namespace DownloadManagerH.Models.Logging
{
    /// <summary>
    /// سطوح مختلف لاگ‌گیری
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// اطلاعات اشکال‌زدایی - جزئی‌ترین سطح
        /// </summary>
        Debug = 0,

        /// <summary>
        /// اطلاعات عمومی - رویدادهای عادی
        /// </summary>
        Info = 1,

        /// <summary>
        /// هشدارها - مسائل غیرحیاتی
        /// </summary>
        Warning = 2,

        /// <summary>
        /// خطاها - مسائل حیاتی
        /// </summary>
        Error = 3
    }
}