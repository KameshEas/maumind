namespace MauMind.App.Services;

/// <summary>
/// Enhanced Voice Service Interface with Speech-to-Text and Text-to-Speech
/// </summary>
public interface IVoiceService
{
    // Speech Recognition (Speech-to-Text)
    Task<bool> IsSpeechSupportedAsync();
    Task<string?> ListenAsync(CancellationToken cancellationToken = default);
    
    // Text-to-Speech
    Task<bool> IsTextToSpeechSupportedAsync();
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
    void StopSpeaking();
    
    // Continuous Listening
    Task StartContinuousListeningAsync(CancellationToken cancellationToken = default);
    void StopListening();
    
    // State Properties
    bool IsListening { get; }
    bool IsSpeaking { get; }
    
    // Events
    event EventHandler<string>? TranscriptionReceived;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler<bool>? ListeningStateChanged;
    event EventHandler<bool>? SpeakingStateChanged;
}
