using System.Windows;
using System.IO;
using System.Text.Json;

namespace DownloadManager.Models;

/// <summary>
/// مدیر تم برای تغییر و مدیریت تم‌های برنامه
/// </summary>
public class ThemeManager
{
    private static ThemeManager? _instance;
    private static readonly object _lock = new();
    
    private readonly Dictionary<string, ThemeConfiguration> _themes = new();
    private ThemeConfiguration? _currentTheme;
    private readonly string _themesConfigPath;

    public static ThemeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ThemeManager();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// رویداد تغییر تم
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// تم فعلی
    /// </summary>
    public ThemeConfiguration? CurrentTheme => _currentTheme;

    /// <summary>
    /// لیست تمام تم‌های موجود
    /// </summary>
    public IReadOnlyDictionary<string, ThemeConfiguration> AvailableThemes => _themes;

    private ThemeManager()
    {
        _themesConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "themes.json");
        InitializeDefaultThemes();
        LoadThemesConfiguration();
    }

    /// <summary>
    /// مقداردهی اولیه تم‌های پیش‌فرض
    /// </summary>
    private void InitializeDefaultThemes()
    {
        // تم مدرن بهبود یافته
        var modernEnhancedTheme = new ThemeConfiguration
        {
            Name = "ModernEnhanced",
            DisplayName = "مدرن پیشرفته",
            ResourcePath = "res/ModernThemeEnhanced.xaml",
            Type = ThemeType.Modern,
            IsDefault = true
        };
        modernEnhancedTheme.SetProperty("AccentColor", "#FF007ACC");
        modernEnhancedTheme.SetProperty("BackgroundColor", "#FFFFFF");
        modernEnhancedTheme.SetProperty("HasAnimations", true);
        modernEnhancedTheme.SetProperty("HasGradients", true);
        _themes[modernEnhancedTheme.Name] = modernEnhancedTheme;

        // تم مدرن کلاسیک
        var modernTheme = new ThemeConfiguration
        {
            Name = "Modern",
            DisplayName = "مدرن",
            ResourcePath = "res/ModernTheme.xaml",
            Type = ThemeType.Modern,
            IsDefault = false
        };
        modernTheme.SetProperty("AccentColor", "#FF007ACC");
        modernTheme.SetProperty("BackgroundColor", "#FFFFFF");
        _themes[modernTheme.Name] = modernTheme;

        // تم تاریک
        var darkTheme = new ThemeConfiguration
        {
            Name = "Dark",
            DisplayName = "تاریک",
            ResourcePath = "res/DarkTheme.xaml",
            Type = ThemeType.Dark,
            IsDefault = false
        };
        darkTheme.SetProperty("AccentColor", "#FF4A9EE7");
        darkTheme.SetProperty("BackgroundColor", "#FF1E1E1E");
        darkTheme.SetProperty("HasAnimations", true);
        darkTheme.SetProperty("HasGradients", true);
        _themes[darkTheme.Name] = darkTheme;

        // تم کلاسیک
        var classicTheme = new ThemeConfiguration
        {
            Name = "Classic",
            DisplayName = "کلاسیک",
            ResourcePath = "res/ClassicTheme.xaml",
            Type = ThemeType.Classic,
            IsDefault = false
        };
        classicTheme.SetProperty("AccentColor", "#FF0078D4");
        classicTheme.SetProperty("BackgroundColor", "#FFF0F0F0");
        _themes[classicTheme.Name] = classicTheme;
    }

    /// <summary>
    /// بارگذاری پیکربندی تم‌ها از فایل
    /// </summary>
    private void LoadThemesConfiguration()
    {
        try
        {
            if (File.Exists(_themesConfigPath))
            {
                var json = File.ReadAllText(_themesConfigPath);
                var configs = JsonSerializer.Deserialize<ThemeConfiguration[]>(json);
                
                if (configs != null)
                {
                    foreach (var config in configs.Where(c => c.IsValid()))
                    {
                        _themes[config.Name] = config;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // در صورت خطا، از تم‌های پیش‌فرض استفاده می‌کنیم
            System.Diagnostics.Debug.WriteLine($"خطا در بارگذاری پیکربندی تم‌ها: {ex.Message}");
        }
    }

    /// <summary>
    /// ذخیره پیکربندی تم‌ها در فایل
    /// </summary>
    private void SaveThemesConfiguration()
    {
        try
        {
            var json = JsonSerializer.Serialize(_themes.Values.ToArray(), new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(_themesConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"خطا در ذخیره پیکربندی تم‌ها: {ex.Message}");
        }
    }

    /// <summary>
    /// اعمال تم
    /// </summary>
    public bool ApplyTheme(string themeName)
    {
        if (!_themes.TryGetValue(themeName, out var theme))
        {
            return false;
        }

        try
        {
            var resources = theme.LoadResources();
            var app = Application.Current;
            
            if (app?.Resources != null)
            {
                // حذف منابع تم قبلی
                RemoveCurrentThemeResources();
                
                // اضافه کردن منابع تم جدید
                app.Resources.MergedDictionaries.Add(resources);
                
                var oldTheme = _currentTheme;
                _currentTheme = theme;
                
                // اطلاع‌رسانی تغییر تم
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, _currentTheme));
                
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"خطا در اعمال تم {themeName}: {ex.Message}");
        }
        
        return false;
    }

    /// <summary>
    /// حذف منابع تم فعلی
    /// </summary>
    private void RemoveCurrentThemeResources()
    {
        var app = Application.Current;
        if (app?.Resources?.MergedDictionaries == null || _currentTheme == null)
            return;

        try
        {
            var currentResources = _currentTheme.LoadResources();
            var toRemove = app.Resources.MergedDictionaries
                .Where(rd => rd.Source?.ToString() == _currentTheme.ResourcePath)
                .ToList();

            foreach (var resource in toRemove)
            {
                app.Resources.MergedDictionaries.Remove(resource);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"خطا در حذف منابع تم: {ex.Message}");
        }
    }

    /// <summary>
    /// اعمال تم پیش‌فرض
    /// </summary>
    public void ApplyDefaultTheme()
    {
        var defaultTheme = _themes.Values.FirstOrDefault(t => t.IsDefault) ?? _themes.Values.First();
        ApplyTheme(defaultTheme.Name);
    }

    /// <summary>
    /// افزودن تم جدید
    /// </summary>
    public bool AddTheme(ThemeConfiguration theme)
    {
        if (!theme.IsValid())
            return false;

        _themes[theme.Name] = theme;
        SaveThemesConfiguration();
        return true;
    }

    /// <summary>
    /// حذف تم
    /// </summary>
    public bool RemoveTheme(string themeName)
    {
        if (_themes.TryGetValue(themeName, out var theme) && !theme.IsDefault)
        {
            _themes.Remove(themeName);
            SaveThemesConfiguration();
            
            // اگر تم فعلی حذف شد، تم پیش‌فرض را اعمال کن
            if (_currentTheme?.Name == themeName)
            {
                ApplyDefaultTheme();
            }
            
            return true;
        }
        return false;
    }

    /// <summary>
    /// دریافت تم بر اساس نام
    /// </summary>
    public ThemeConfiguration? GetTheme(string themeName)
    {
        _themes.TryGetValue(themeName, out var theme);
        return theme;
    }

    /// <summary>
    /// بررسی وجود تم
    /// </summary>
    public bool HasTheme(string themeName)
    {
        return _themes.ContainsKey(themeName);
    }

    /// <summary>
    /// تنظیم تم پیش‌فرض
    /// </summary>
    public void SetDefaultTheme(string themeName)
    {
        if (_themes.TryGetValue(themeName, out var theme))
        {
            // حذف پیش‌فرض بودن از سایر تم‌ها
            foreach (var t in _themes.Values)
            {
                t.IsDefault = false;
            }
            
            theme.IsDefault = true;
            SaveThemesConfiguration();
        }
    }
}

/// <summary>
/// آرگومان‌های رویداد تغییر تم
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public ThemeConfiguration? OldTheme { get; }
    public ThemeConfiguration NewTheme { get; }

    public ThemeChangedEventArgs(ThemeConfiguration? oldTheme, ThemeConfiguration newTheme)
    {
        OldTheme = oldTheme;
        NewTheme = newTheme;
    }
}