using System.Diagnostics;

namespace MauMind.App.Services;

/// <summary>
/// Service for providing haptic feedback on mobile devices
/// </summary>
public interface IHapticService
{
    /// <summary>
    /// Light impact - for UI selections, small interactions
    /// </summary>
    void LightImpact();
    
    /// <summary>
    /// Medium impact - for button presses, confirmations
    /// </summary>
    void MediumImpact();
    
    /// <summary>
    /// Heavy impact - for important actions, deletions
    /// </summary>
    void HeavyImpact();
    
    /// <summary>
    /// Selection changed feedback
    /// </summary>
    void SelectionChanged();
    
    /// <summary>
    /// Success notification - triple light impact
    /// </summary>
    void Success();
    
    /// <summary>
    /// Warning notification
    /// </summary>
    void Warning();
    
    /// <summary>
    /// Error notification - heavy impact
    /// </summary>
    void Error();
    
    /// <summary>
    /// Custom vibrate pattern
    /// </summary>
    void Vibrate(int milliseconds = 100);
}

public class HapticService : IHapticService
{
    public void LightImpact()
    {
        try
        {
#if ANDROID
            var vibrator = (Android.OS.Vibrator)Android.App.Application.Context.GetSystemService(Android.Content.Context.VibratorService);
            if (vibrator != null && vibrator.HasVibrator)
            {
                vibrator.Vibrate(10);
            }
#elif IOS
            // iOS UIImpactFeedbackGenerator would go here
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Haptic LightImpact error: {ex.Message}");
        }
    }
    
    public void MediumImpact()
    {
        try
        {
#if ANDROID
            var vibrator = (Android.OS.Vibrator)Android.App.Application.Context.GetSystemService(Android.Content.Context.VibratorService);
            if (vibrator != null && vibrator.HasVibrator)
            {
                vibrator.Vibrate(25);
            }
#elif IOS
            // iOS UIImpactFeedbackGenerator.Medium
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Haptic MediumImpact error: {ex.Message}");
        }
    }
    
    public void HeavyImpact()
    {
        try
        {
#if ANDROID
            var vibrator = (Android.OS.Vibrator)Android.App.Application.Context.GetSystemService(Android.Content.Context.VibratorService);
            if (vibrator != null && vibrator.HasVibrator)
            {
                vibrator.Vibrate(50);
            }
#elif IOS
            // iOS UIImpactFeedbackGenerator.Heavy
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Haptic HeavyImpact error: {ex.Message}");
        }
    }
    
    public void SelectionChanged()
    {
        try
        {
#if ANDROID
            // On Android, this is typically handled by the system
#elif IOS
            // iOS UISelectionFeedbackGenerator
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Haptic SelectionChanged error: {ex.Message}");
        }
    }
    
    public void Success()
    {
        // Triple light impact for success
        LightImpact();
        Task.Delay(50).ContinueWith(_ => LightImpact());
        Task.Delay(100).ContinueWith(_ => LightImpact());
    }
    
    public void Warning()
    {
        // Double medium impact for warning
        MediumImpact();
        Task.Delay(75).ContinueWith(_ => MediumImpact());
    }
    
    public void Error()
    {
        // Heavy impact for error
        HeavyImpact();
    }
    
    public void Vibrate(int milliseconds = 100)
    {
        try
        {
#if ANDROID
            var vibrator = (Android.OS.Vibrator)Android.App.Application.Context.GetSystemService(Android.Content.Context.VibratorService);
            if (vibrator != null && vibrator.HasVibrator)
            {
                vibrator.Vibrate(milliseconds);
            }
#elif IOS
            // iOS UINotificationFeedbackGenerator
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Haptic Vibrate error: {ex.Message}");
        }
    }
}
