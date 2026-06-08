namespace DownloadManager.Models;

/// <summary>
/// رابط برای کلاس‌هایی که باید از تغییر تم آگاه شوند
/// </summary>
public interface IThemeAware
{
    /// <summary>
    /// فراخوانی هنگام تغییر تم
    /// </summary>
    /// <param name="newTheme">تم جدید</param>
    void OnThemeChanged(ThemeConfiguration newTheme);
}