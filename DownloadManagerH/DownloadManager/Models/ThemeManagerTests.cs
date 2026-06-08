using System.Windows;

namespace DownloadManager.Models;

/// <summary>
/// تست‌های واحد برای ThemeManager
/// </summary>
public class ThemeManagerTests
{
    private ThemeManager _themeManager;

    public ThemeManagerTests()
    {
        _themeManager = ThemeManager.Instance;
    }

    /// <summary>
    /// تست مقداردهی اولیه تم‌های پیش‌فرض
    /// </summary>
    public bool TestDefaultThemesInitialization()
    {
        try
        {
            var themes = _themeManager.AvailableThemes;
            
            // بررسی وجود تم‌های پیش‌فرض
            if (!themes.ContainsKey("Modern") || !themes.ContainsKey("Classic"))
            {
                return false;
            }

            // بررسی تم پیش‌فرض
            var defaultTheme = themes.Values.FirstOrDefault(t => t.IsDefault);
            if (defaultTheme == null || defaultTheme.Name != "Modern")
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// تست افزودن تم جدید
    /// </summary>
    public bool TestAddNewTheme()
    {
        try
        {
            var newTheme = new ThemeConfiguration
            {
                Name = "TestTheme",
                DisplayName = "تم تست",
                ResourcePath = "res/TestTheme.xaml",
                Type = ThemeType.Light
            };

            var result = _themeManager.AddTheme(newTheme);
            if (!result)
                return false;

            // بررسی اضافه شدن تم
            var addedTheme = _themeManager.GetTheme("TestTheme");
            return addedTheme != null && addedTheme.DisplayName == "تم تست";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// تست حذف تم
    /// </summary>
    public bool TestRemoveTheme()
    {
        try
        {
            // ابتدا تم تست را اضافه کنیم
            var testTheme = new ThemeConfiguration
            {
                Name = "RemoveTestTheme",
                DisplayName = "تم تست حذف",
                ResourcePath = "res/TestTheme.xaml",
                Type = ThemeType.Light
            };

            _themeManager.AddTheme(testTheme);

            // حالا آن را حذف کنیم
            var result = _themeManager.RemoveTheme("RemoveTestTheme");
            if (!result)
                return false;

            // بررسی حذف شدن
            return !_themeManager.HasTheme("RemoveTestTheme");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// تست اعتبارسنجی تم
    /// </summary>
    public bool TestThemeValidation()
    {
        try
        {
            // تم معتبر
            var validTheme = new ThemeConfiguration
            {
                Name = "ValidTheme",
                DisplayName = "تم معتبر",
                ResourcePath = "res/ValidTheme.xaml",
                Type = ThemeType.Modern
            };

            if (!validTheme.IsValid())
                return false;

            // تم نامعتبر (بدون نام)
            var invalidTheme = new ThemeConfiguration
            {
                DisplayName = "تم نامعتبر",
                ResourcePath = "res/InvalidTheme.xaml",
                Type = ThemeType.Modern
            };

            return !invalidTheme.IsValid();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// تست خصوصیات تم
    /// </summary>
    public bool TestThemeProperties()
    {
        try
        {
            var theme = new ThemeConfiguration
            {
                Name = "PropertyTestTheme",
                DisplayName = "تم تست خصوصیات",
                ResourcePath = "res/PropertyTestTheme.xaml",
                Type = ThemeType.Modern
            };

            // تنظیم خصوصیت
            theme.SetProperty("TestColor", "#FF0000");
            theme.SetProperty("TestSize", 16);

            // دریافت خصوصیت
            var color = theme.GetProperty<string>("TestColor");
            var size = theme.GetProperty<int>("TestSize");

            if (color != "#FF0000" || size != 16)
                return false;

            // دریافت خصوصیت غیرموجود با مقدار پیش‌فرض
            var nonExistent = theme.GetProperty("NonExistent", "Default");
            return nonExistent == "Default";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// تست تنظیم تم پیش‌فرض
    /// </summary>
    public bool TestSetDefaultTheme()
    {
        try
        {
            // تنظیم تم کلاسیک به عنوان پیش‌فرض
            _themeManager.SetDefaultTheme("Classic");

            var classicTheme = _themeManager.GetTheme("Classic");
            var modernTheme = _themeManager.GetTheme("Modern");

            if (classicTheme == null || modernTheme == null)
                return false;

            // بررسی تغییر پیش‌فرض
            if (!classicTheme.IsDefault || modernTheme.IsDefault)
                return false;

            // بازگرداندن تم مدرن به حالت پیش‌فرض
            _themeManager.SetDefaultTheme("Modern");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// اجرای تمام تست‌ها
    /// </summary>
    public Dictionary<string, bool> RunAllTests()
    {
        var results = new Dictionary<string, bool>();

        results["DefaultThemesInitialization"] = TestDefaultThemesInitialization();
        results["AddNewTheme"] = TestAddNewTheme();
        results["RemoveTheme"] = TestRemoveTheme();
        results["ThemeValidation"] = TestThemeValidation();
        results["ThemeProperties"] = TestThemeProperties();
        results["SetDefaultTheme"] = TestSetDefaultTheme();

        return results;
    }

    /// <summary>
    /// نمایش نتایج تست
    /// </summary>
    public string GetTestResults()
    {
        var results = RunAllTests();
        var passed = results.Count(r => r.Value);
        var total = results.Count;

        var report = $"نتایج تست ThemeManager:\n";
        report += $"تعداد کل تست‌ها: {total}\n";
        report += $"تست‌های موفق: {passed}\n";
        report += $"تست‌های ناموفق: {total - passed}\n\n";

        foreach (var result in results)
        {
            var status = result.Value ? "✅ موفق" : "❌ ناموفق";
            report += $"{result.Key}: {status}\n";
        }

        return report;
    }
}