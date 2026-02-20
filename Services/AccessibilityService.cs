using Microsoft.Maui.Controls;

namespace MauMind.App.Services;

public interface IAccessibilityService
{
    bool IsScreenReaderEnabled { get; }
    bool IsHighContrastMode { get; }
    void Announce(string message);
    void SetHighContrastMode(bool enabled);
    event EventHandler? AccessibilitySettingsChanged;
}

public class AccessibilityService : IAccessibilityService
{
    private bool _isHighContrastMode;
    
    public event EventHandler? AccessibilitySettingsChanged;
    
    public bool IsScreenReaderEnabled => false; // Check via AutomationProperties
    public bool IsHighContrastMode => _isHighContrastMode;
    
    public AccessibilityService()
    {
        // High contrast mode is disabled by default
        _isHighContrastMode = false;
    }
    
    public void Announce(string message)
    {
        // In MAUI, screen reader announcement is handled via SemanticProperties
        // This method can be used to trigger accessibility announcements
        System.Diagnostics.Debug.WriteLine($"Accessibility: {message}");
    }
    
    public void SetHighContrastMode(bool enabled)
    {
        _isHighContrastMode = enabled;
        
        // Apply high contrast colors
        var app = Application.Current;
        if (app == null) return;
        
        if (_isHighContrastMode)
        {
            // High contrast colors - black background, yellow/green text
            app.Resources["BackgroundColor"] = Color.FromRgb(0, 0, 0);
            app.Resources["SurfaceColor"] = Color.FromRgb(0, 0, 0);
            app.Resources["CardColor"] = Color.FromRgb(30, 30, 30);
            app.Resources["TextPrimary"] = Color.FromRgb(255, 255, 255);
            app.Resources["TextSecondary"] = Color.FromRgb(255, 255, 0);
            app.Resources["TextTertiary"] = Color.FromRgb(0, 255, 0);
            app.Resources["AIBubbleColor"] = Color.FromRgb(30, 30, 30);
            app.Resources["AITextColor"] = Color.FromRgb(255, 255, 255);
            app.Resources["TabBarBackground"] = Color.FromRgb(0, 0, 0);
            app.Resources["UserBubbleColor"] = Color.FromRgb(0, 0, 255);
        }
        
        AccessibilitySettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
