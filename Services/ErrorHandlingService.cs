namespace MauMind.App.Services;

public interface IErrorHandlingService
{
    void HandleError(Exception ex, string context);
    void ShowError(string title, string message);
    Task<bool> ShowRetryDialogAsync(string title, string message);
    void ShowToast(string message, bool isError = false);
    string GetUserFriendlyMessage(Exception ex);
}

public class ErrorHandlingService : IErrorHandlingService
{
    public void HandleError(Exception ex, string context)
    {
        // Log the error for debugging
        System.Diagnostics.Debug.WriteLine($"Error in {context}: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        
        // Show user-friendly error
        var message = GetUserFriendlyMessage(ex);
        ShowError("Error", $"{message}\n\nContext: {context}");
    }
    
    public void ShowError(string title, string message)
    {
        // In a real app, this would show a dialog
        // For now, we'll use a simple approach
        System.Diagnostics.Debug.WriteLine($"[ERROR] {title}: {message}");
    }
    
    public async Task<bool> ShowRetryDialogAsync(string title, string message)
    {
        // This would typically show a dialog with Retry/Cancel buttons
        // For now, we'll return false to let the caller decide
        System.Diagnostics.Debug.WriteLine($"[RETRY] {title}: {message}");
        
        // In MAUI, you'd use:
        // var result = await DisplayAlert(title, message, "Retry", "Cancel");
        // return result;
        
        return await Task.FromResult(false);
    }
    
    public void ShowToast(string message, bool isError = false)
    {
        // Toast notification - would use MAUI's Toast in production
        System.Diagnostics.Debug.WriteLine($"[TOAST] {(isError ? "ERROR" : "INFO")}: {message}");
    }
    
    public string GetUserFriendlyMessage(Exception ex)
    {
        // Convert technical errors to user-friendly messages
        return ex switch
        {
            // AI/ML Errors
            var e when e.Message.Contains("ONNX") || e.Message.Contains("model") =>
                "Unable to load AI model. Please check that the model files are valid.",
            
            var e when e.Message.Contains("out of memory") || e.Message.Contains("OOM") =>
                "Your device is running low on memory. Please try closing other apps.",
            
            var e when e.Message.Contains("timeout") || e.Message.Contains("timed out") =>
                "The operation took too long. Please check your device's performance.",
            
            // Network Errors
            var e when e.Message.Contains("network") || e.Message.Contains("connection") =>
                "Network error. Please check your internet connection.",
            
            // File Errors
            var e when e.Message.Contains("file") || e.Message.Contains("not found") =>
                "Unable to access the requested file. It may have been moved or deleted.",
            
            var e when e.Message.Contains("permission") || e.Message.Contains("access") =>
                "Permission denied. Please check app permissions in Settings.",
            
            // Database Errors
            var e when e.Message.Contains("database") || e.Message.Contains("SQL") =>
                "Database error. Please try restarting the app.",
            
            // Default
            _ => "Something went wrong. Please try again. If the problem persists, restart the app."
        };
    }
}

// Extension methods for handling common async operations with retry
public static class RetryHelper
{
    public static async Task<T> RetryWithBackoff<T>(
        this Func<Task<T>> action,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        Action<Exception, int>? onRetry = null)
    {
        var delay = initialDelayMs;
        
        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (i < maxRetries)
            {
                onRetry?.Invoke(ex, i + 1);
                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
        }
        
        return await action(); // Final attempt - let exception propagate
    }
    
    public static async Task RetryWithBackoff(
        this Func<Task> action,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        Action<Exception, int>? onRetry = null)
    {
        var delay = initialDelayMs;
        
        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (i < maxRetries)
            {
                onRetry?.Invoke(ex, i + 1);
                await Task.Delay(delay);
                delay *= 2;
            }
        }
        
        await action();
    }
}
