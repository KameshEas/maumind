using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MauMind.App.Services;

public interface IThemeService
{
    bool IsDarkMode { get; }
    bool IsSystemTheme { get; }
    void SetTheme(bool isDark);
    void SetSystemTheme();
    void ToggleTheme();
    event EventHandler? ThemeChanged;
}

public class ThemeService : IThemeService
{
    private bool _isDarkMode;
    private bool _useSystemTheme = true;
    
    public event EventHandler? ThemeChanged;
    
    public bool IsDarkMode => _isDarkMode;
    public bool IsSystemTheme => _useSystemTheme;
    
    public ThemeService()
    {
        // Try to detect system theme
        DetectSystemTheme();
        
        // Listen for system theme changes
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnSystemThemeChanged;
        }
    }
    
    private void DetectSystemTheme()
    {
        try
        {
            var app = Application.Current;
            if (app != null)
            {
                _isDarkMode = app.RequestedTheme == AppTheme.Dark;
                _useSystemTheme = true;
            }
        }
        catch
        {
            _isDarkMode = false;
            _useSystemTheme = false;
        }
    }
    
    private void OnSystemThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (_useSystemTheme)
        {
            _isDarkMode = e.RequestedTheme == AppTheme.Dark;
            ApplyTheme();
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public void SetTheme(bool isDark)
    {
        _isDarkMode = isDark;
        _useSystemTheme = false;
        ApplyTheme();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public void SetSystemTheme()
    {
        _useSystemTheme = true;
        DetectSystemTheme();
        ApplyTheme();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public void ToggleTheme()
    {
        _isDarkMode = !_isDarkMode;
        _useSystemTheme = false;
        ApplyTheme();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
    
    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;
        
        // Set the app theme
        app.UserAppTheme = _isDarkMode ? AppTheme.Dark : AppTheme.Light;
        
        // Apply custom colors to resources
        if (_isDarkMode)
        {
            // Dark Theme Colors
            app.Resources["BackgroundColor"] = Color.FromRgb(18, 18, 18);
            app.Resources["SurfaceColor"] = Color.FromRgb(30, 30, 30);
            app.Resources["CardColor"] = Color.FromRgb(40, 40, 40);
            app.Resources["TextPrimary"] = Color.FromRgb(255, 255, 255);
            app.Resources["TextSecondary"] = Color.FromRgb(180, 180, 180);
            app.Resources["TextTertiary"] = Color.FromRgb(120, 120, 120);
            app.Resources["AIBubbleColor"] = Color.FromRgb(40, 40, 40);
            app.Resources["AITextColor"] = Color.FromRgb(255, 255, 255);
            app.Resources["TabBarBackground"] = Color.FromRgb(30, 30, 30);
            app.Resources["UserBubbleColor"] = Color.FromRgb(0, 100, 200);
            app.Resources["PageBackgroundLightBrush"] = new SolidColorBrush(Colors.Transparent);
            app.Resources["PageBackgroundDarkBrush"] = new SolidColorBrush(Color.FromRgb(18, 18, 18));
            app.Resources["CardBackgroundLightBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            app.Resources["CardBackgroundDarkBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            app.Resources["TextPrimaryLightBrush"] = new SolidColorBrush(Colors.White);
            app.Resources["TextPrimaryDarkBrush"] = new SolidColorBrush(Colors.White);
            app.Resources["TextSecondaryLightBrush"] = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            app.Resources["TextSecondaryDarkBrush"] = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            app.Resources["TextTertiaryLight"] = Color.FromRgb(120, 120, 120);
            app.Resources["TextTertiaryDark"] = Color.FromRgb(120, 120, 120);
        }
        else
        {
            // Light Theme Colors
            app.Resources["BackgroundColor"] = Color.FromRgb(245, 245, 245);
            app.Resources["SurfaceColor"] = Color.FromRgb(255, 255, 255);
            app.Resources["CardColor"] = Color.FromRgb(255, 255, 255);
            app.Resources["TextPrimary"] = Color.FromRgb(51, 51, 51);
            app.Resources["TextSecondary"] = Color.FromRgb(102, 102, 102);
            app.Resources["TextTertiary"] = Color.FromRgb(153, 153, 153);
            app.Resources["AIBubbleColor"] = Color.FromRgb(255, 255, 255);
            app.Resources["AITextColor"] = Color.FromRgb(51, 51, 51);
            app.Resources["TabBarBackground"] = Color.FromRgb(255, 255, 255);
            app.Resources["UserBubbleColor"] = Color.FromRgb(0, 120, 212);
            app.Resources["PageBackgroundLightBrush"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            app.Resources["PageBackgroundDarkBrush"] = new SolidColorBrush(Colors.Transparent);
            app.Resources["CardBackgroundLightBrush"] = new SolidColorBrush(Colors.White);
            app.Resources["CardBackgroundDarkBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            app.Resources["TextPrimaryLightBrush"] = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            app.Resources["TextPrimaryDarkBrush"] = new SolidColorBrush(Colors.White);
            app.Resources["TextSecondaryLightBrush"] = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            app.Resources["TextSecondaryDarkBrush"] = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            app.Resources["TextTertiaryLight"] = Color.FromRgb(153, 153, 153);
            app.Resources["TextTertiaryDark"] = Color.FromRgb(120, 120, 120);
        }
    }
}
