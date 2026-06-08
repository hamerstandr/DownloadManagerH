namespace DownloadManagerH.Models
{
    /// <summary>
    /// اولویت دانلود
    /// </summary>
    public enum DownloadPriority
    {
        /// <summary>
        /// اولویت پایین
        /// </summary>
        Low = 0,

        /// <summary>
        /// اولویت عادی
        /// </summary>
        Normal = 1,

        /// <summary>
        /// اولویت بالا
        /// </summary>
        High = 2,

        /// <summary>
        /// اولویت فوری
        /// </summary>
        Urgent = 3
    }
}