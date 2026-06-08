using System.Windows;

namespace DownloadManager.Models;

/// <summary>
/// کلاس پیکربندی تم برای تعریف خصوصیات هر تم
/// </summary>
public class ThemeConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ResourcePath { get; set; } = string.Empty;
    public ThemeType Type { get; set; }
    public bool IsDefault { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// بارگذاری منابع تم
    /// </summary>
    public ResourceDictionary LoadResources()
    {
        try
        {
            var uri = new Uri(ResourcePath, UriKind.Relative);
            return (ResourceDictionary)Application.LoadComponent(uri);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"خطا در بارگذاری منابع تم {Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// اعتبارسنجی پیکربندی تم
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Name) && 
               !string.IsNullOrEmpty(DisplayName) && 
               !string.IsNullOrEmpty(ResourcePath);
    }

    /// <summary>
    /// دریافت خصوصیت تم
    /// </summary>
    public T? GetProperty<T>(string key, T? defaultValue = default)
    {
        if (Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// تنظیم خصوصیت تم
    /// </summary>
    public void SetProperty<T>(string key, T value)
    {
        if (value != null)
        {
            Properties[key] = value;
        }
    }
}

/// <summary>
/// انواع تم
/// </summary>
public enum ThemeType
{
    Light,
    Dark,
    Modern,
    Classic
}