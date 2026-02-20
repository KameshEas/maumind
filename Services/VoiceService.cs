using System.Diagnostics;

namespace MauMind.App.Services;

public class VoiceService : IVoiceService, IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _isListening;
    
    public event EventHandler<string>? TranscriptionReceived;
    public event EventHandler<string>? ErrorOccurred;
    
    public Task<bool> IsSpeechSupportedAsync()
    {
        return Task.FromResult(true);
    }
    
    public async Task<string?> ListenAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            return null;
        }
        
        _isListening = true;
        
        try
        {
#if ANDROID
            return await ListenAndroidAsync();
#elif IOS
            return await Task.FromResult<string?>(null);
#else
            return await Task.FromResult<string?>(null);
#endif
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            return null;
        }
        finally
        {
            _isListening = false;
        }
    }
    
#if ANDROID
    private Task<string?> ListenAndroidAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        
        try
        {
            // Get the activity context
            var context = Android.App.Application.Context;
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            
            if (activity == null)
            {
                return Task.FromResult<string?>(null);
            }
            
            // Create speech recognition intent
            var intent = new Android.Content.Intent(Android.Speech.RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraLanguageModel, Android.Speech.RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraPrompt, "Speak now...");
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraMaxResults, 1);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraLanguage, "en-US");
            
            // Use a simplified approach - launch the recognizer
            // The result will come back via OnActivityResult
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.StartActivityForResult(
                intent, 100);
            
            // For now, return a message that voice input is ready
            // In production, you'd use a SpeechRecognitionListener
            return Task.FromResult<string?>("Voice recognition started. Please speak clearly.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Android speech error: {ex.Message}");
            return Task.FromResult<string?>(null);
        }
    }
#endif
    
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
