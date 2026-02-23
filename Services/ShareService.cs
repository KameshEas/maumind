using System.Diagnostics;

namespace MauMind.App.Services;

/// <summary>
/// Service for handling shared content from other apps
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Share text content to other apps
    /// </summary>
    Task ShareTextAsync(string text, string title = "Share");
    
    /// <summary>
    /// Share a file to other apps
    /// </summary>
    Task ShareFileAsync(string filePath, string title = "Share");
    
    /// <summary>
    /// Receive shared text (for Share Extension)
    /// </summary>
    Task<string?> GetSharedTextAsync();
    
    /// <summary>
    /// Check if there's pending shared content
    /// </summary>
    bool HasSharedContent { get; }
}

public class ShareService : IShareService
{
    public bool HasSharedContent { get; private set; }
    private string? _sharedText;
    
    public async Task ShareTextAsync(string text, string title = "Share")
    {
        try
        {
#if ANDROID
            await ShareTextAndroidAsync(text, title);
#elif IOS
            await ShareTextIOSAsync(text, title);
#else
            await Task.Delay(100);
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareService ShareTextAsync error: {ex.Message}");
        }
    }
    
    public async Task ShareFileAsync(string filePath, string title = "Share")
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"ShareService: File not found - {filePath}");
                return;
            }
            
#if ANDROID
            await ShareFileAndroidAsync(filePath, title);
#elif IOS
            await ShareFileIOSAsync(filePath, title);
#else
            await Task.Delay(100);
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareService ShareFileAsync error: {ex.Message}");
        }
    }
    
    public Task<string?> GetSharedTextAsync()
    {
        // This would be called when app launches with shared content
        // The actual implementation depends on platform-specific handlers
        if (HasSharedContent)
        {
            HasSharedContent = false;
            return Task.FromResult<string?>(_sharedText);
        }
        return Task.FromResult<string?>(null);
    }
    
    public void SetSharedContent(string text)
    {
        _sharedText = text;
        HasSharedContent = true;
    }

#if ANDROID
    private Task ShareTextAndroidAsync(string text, string title)
    {
        try
        {
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionSend);
            intent.SetType("text/plain");
            intent.PutExtra(Android.Content.Intent.ExtraText, text);
            intent.PutExtra(Android.Content.Intent.ExtraTitle, title);
            
            var chooser = Android.Content.Intent.CreateChooser(intent, title);
            chooser.AddFlags(Android.Content.ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(chooser);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Android share error: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    
    private Task ShareFileAndroidAsync(string filePath, string title)
    {
        try
        {
            var file = new Java.IO.File(filePath);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                Android.App.Application.Context,
                "${ApplicationId}.fileprovider",
                file);
            
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionSend);
            intent.SetType("*/*");
            intent.PutExtra(Android.Content.Intent.ExtraStream, uri);
            intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
            
            var chooser = Android.Content.Intent.CreateChooser(intent, title);
            chooser.AddFlags(Android.Content.ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(chooser);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Android file share error: {ex.Message}");
        }
        return Task.CompletedTask;
    }
#endif

#if IOS
    private Task ShareTextIOSAsync(string text, string title)
    {
        // iOS UIActivityViewController would go here
        Debug.WriteLine($"iOS Share: {text}");
        return Task.CompletedTask;
    }
    
    private Task ShareFileIOSAsync(string filePath, string title)
    {
        // iOS UIActivityViewController would go here
        Debug.WriteLine($"iOS Share File: {filePath}");
        return Task.CompletedTask;
    }
#endif
}
