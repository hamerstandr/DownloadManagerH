using System.Windows;

namespace DownloadManager.Models;

/// <summary>
/// کلاس تست برای اجرای تست‌های ThemeManager
/// </summary>
public static class ThemeTestRunner
{
    /// <summary>
    /// اجرای تست‌های ThemeManager
    /// </summary>
    public static void RunTests()
    {
        try
        {
            var tests = new ThemeManagerTests();
            var results = tests.RunAllTests();
            
            Console.WriteLine("=== نتایج تست ThemeManager ===");
            Console.WriteLine($"تعداد کل تست‌ها: {results.Count}");
            Console.WriteLine($"تست‌های موفق: {results.Count(r => r.Value)}");
            Console.WriteLine($"تست‌های ناموفق: {results.Count(r => !r.Value)}");
            Console.WriteLine();
            
            foreach (var result in results)
            {
                var status = result.Value ? "✅ موفق" : "❌ ناموفق";
                Console.WriteLine($"{result.Key}: {status}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== تست تم‌های موجود ===");
            var themeManager = ThemeManager.Instance;
            
            foreach (var theme in themeManager.AvailableThemes)
            {
                Console.WriteLine($"تم: {theme.Value.DisplayName} ({theme.Key})");
                Console.WriteLine($"  - نوع: {theme.Value.Type}");
                Console.WriteLine($"  - مسیر: {theme.Value.ResourcePath}");
                Console.WriteLine($"  - پیش‌فرض: {(theme.Value.IsDefault ? "بله" : "خیر")}");
                
                // تست خصوصیات
                var accentColor = theme.Value.GetProperty<string>("AccentColor");
                var backgroundColor = theme.Value.GetProperty<string>("BackgroundColor");
                
                if (!string.IsNullOrEmpty(accentColor))
                    Console.WriteLine($"  - رنگ اصلی: {accentColor}");
                if (!string.IsNullOrEmpty(backgroundColor))
                    Console.WriteLine($"  - رنگ پس‌زمینه: {backgroundColor}");
                
                Console.WriteLine();
            }
            
            // تست اعمال تم‌ها
            Console.WriteLine("=== تست اعمال تم‌ها ===");
            
            foreach (var themeName in themeManager.AvailableThemes.Keys)
            {
                var success = themeManager.ApplyTheme(themeName);
                var status = success ? "✅ موفق" : "❌ ناموفق";
                Console.WriteLine($"اعمال تم {themeName}: {status}");
                
                if (success && themeManager.CurrentTheme != null)
                {
                    Console.WriteLine($"  تم فعلی: {themeManager.CurrentTheme.DisplayName}");
                }
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطا در اجرای تست‌ها: {ex.Message}");
            Console.WriteLine($"جزئیات: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// تست تغییر تم در زمان اجرا
    /// </summary>
    public static void TestThemeSwitching()
    {
        try
        {
            var themeManager = ThemeManager.Instance;
            
            Console.WriteLine("=== تست تغییر تم در زمان اجرا ===");
            
            // ثبت رویداد تغییر تم
            themeManager.ThemeChanged += (sender, e) =>
            {
                Console.WriteLine($"تم تغییر کرد:");
                Console.WriteLine($"  از: {e.OldTheme?.DisplayName ?? "هیچ"}");
                Console.WriteLine($"  به: {e.NewTheme.DisplayName}");
            };
            
            // تست تغییر بین تم‌ها
            var themes = themeManager.AvailableThemes.Keys.ToArray();
            
            foreach (var themeName in themes)
            {
                Console.WriteLine($"تغییر به تم: {themeName}");
                var success = themeManager.ApplyTheme(themeName);
                
                if (success)
                {
                    Console.WriteLine($"  ✅ تم {themeName} با موفقیت اعمال شد");
                    
                    // کمی صبر کنیم تا تغییر اعمال شود
                    System.Threading.Thread.Sleep(500);
                }
                else
                {
                    Console.WriteLine($"  ❌ خطا در اعمال تم {themeName}");
                }
            }
            
            // بازگشت به تم پیش‌فرض
            Console.WriteLine("بازگشت به تم پیش‌فرض...");
            themeManager.ApplyDefaultTheme();
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطا در تست تغییر تم: {ex.Message}");
        }
    }
}