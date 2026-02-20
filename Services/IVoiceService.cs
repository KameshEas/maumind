namespace MauMind.App.Services;

public interface IVoiceService
{
    Task<bool> IsSpeechSupportedAsync();
    Task<string?> ListenAsync(CancellationToken cancellationToken = default);
    event EventHandler<string>? TranscriptionReceived;
    event EventHandler<string>? ErrorOccurred;
}
