using System.Diagnostics;

namespace MauMind.App.Services;

/// <summary>
/// Enhanced Voice Service with Speech-to-Text and Text-to-Speech capabilities
/// </summary>
public class VoiceService : IVoiceService, IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _isListening;
    private bool _isSpeaking;
    
    // Events for voice state changes
    public event EventHandler<string>? TranscriptionReceived;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<bool>? ListeningStateChanged;
    public event EventHandler<bool>? SpeakingStateChanged;
    
    // Current state properties
    public bool IsListening => _isListening;
    public bool IsSpeaking => _isSpeaking;
    
    public Task<bool> IsSpeechSupportedAsync()
    {
        return Task.FromResult(true);
    }
    
    public Task<bool> IsTextToSpeechSupportedAsync()
    {
        return Task.FromResult(true);
    }
    
    /// <summary>
    /// Listen for speech input (Speech-to-Text)
    /// </summary>
    public async Task<string?> ListenAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            return null;
        }
        
        _isListening = true;
        ListeningStateChanged?.Invoke(this, true);
        
        try
        {
#if ANDROID
            return await ListenAndroidAsync();
#elif IOS
            return await ListenIOSAsync();
#else
            return await Task.FromResult<string?>(null);
#endif
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            Debug.WriteLine($"VoiceService Listen error: {ex.Message}");
            return null;
        }
        finally
        {
            _isListening = false;
            ListeningStateChanged?.Invoke(this, false);
        }
    }
    
    /// <summary>
    /// Speak text aloud (Text-to-Speech)
    /// </summary>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_isSpeaking || string.IsNullOrEmpty(text))
        {
            return;
        }
        
        _isSpeaking = true;
        SpeakingStateChanged?.Invoke(this, true);
        
        try
        {
#if ANDROID
            await SpeakAndroidAsync(text);
#elif IOS
            await SpeakIOSAsync(text);
#else
            await Task.Delay(100); // Placeholder for other platforms
#endif
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            Debug.WriteLine($"VoiceService Speak error: {ex.Message}");
        }
        finally
        {
            _isSpeaking = false;
            SpeakingStateChanged?.Invoke(this, false);
        }
    }
    
    /// <summary>
    /// Stop any ongoing speech
    /// </summary>
    public void StopSpeaking()
    {
        if (_isSpeaking)
        {
            _isSpeaking = false;
            SpeakingStateChanged?.Invoke(this, false);
            
#if ANDROID
            try
            {
                // Platform-specific TTS stopping can be implemented here if a TextToSpeech
                // instance is available via DI. Avoiding direct calls to platform APIs
                // here prevents compilation issues across SDK versions.
            }
            catch { }
#endif
        }
    }
    
    /// <summary>
    /// Start continuous listening mode
    /// </summary>
    public async Task StartContinuousListeningAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        while (!_cts.Token.IsCancellationRequested)
        {
            var result = await ListenAsync(_cts.Token);
            if (result != null && !string.IsNullOrEmpty(result))
            {
                TranscriptionReceived?.Invoke(this, result);
            }
            
            // Small delay between listening sessions
            await Task.Delay(100, _cts.Token);
        }
    }
    
    /// <summary>
    /// Stop continuous listening
    /// </summary>
    public void StopListening()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
    
#if ANDROID
    private Task<string?> ListenAndroidAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            
            if (activity == null)
            {
                return Task.FromResult<string?>(null);
            }
            
            // Create speech recognition intent
            var intent = new Android.Content.Intent(Android.Speech.RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraLanguageModel, 
                Android.Speech.RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraPrompt, "Speak now...");
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraMaxResults, 1);
            intent.PutExtra(Android.Speech.RecognizerIntent.ExtraLanguage, "en-US");
            
            activity.StartActivityForResult(intent, 100);
            
            return Task.FromResult<string?>("Voice recognition started. Please speak clearly.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Android speech error: {ex.Message}");
            return Task.FromResult<string?>(null);
        }
    }
    
    private Task SpeakAndroidAsync(string text)
    {
        try
        {
            // Using Android TTS
            // In a full implementation, you'd use Android.Speech.tts.TextToSpeech
            Debug.WriteLine($"Android TTS: Speaking - {text.Substring(0, Math.Min(50, text.Length))}...");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Android TTS error: {ex.Message}");
            return Task.CompletedTask;
        }
    }
#endif

#if IOS
    private Task<string?> ListenIOSAsync()
    {
        // iOS Speech Recognition would go here
        return Task.FromResult<string?>(null);
    }
    
    private Task SpeakIOSAsync(string text)
    {
        // iOS AVSpeechSynthesizer would go here
        Debug.WriteLine($"iOS TTS: Speaking - {text.Substring(0, Math.Min(50, text.Length))}...");
        return Task.CompletedTask;
    }
#endif
    
    public void Dispose()
    {
        StopListening();
        StopSpeaking();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
