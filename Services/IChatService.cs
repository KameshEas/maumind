using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IChatService
{
    IAsyncEnumerable<string> GetStreamingResponseAsync(
        string userMessage, 
        CancellationToken cancellationToken = default);
    
    Task<List<ChatMessage>> GetChatHistoryAsync();
    Task SaveMessageAsync(ChatMessage message);
    Task ClearHistoryAsync();
    bool IsModelLoaded { get; }
    Task LoadModelAsync(IProgress<int>? progress = null);

    /// <summary>
    /// Fired when no local knowledge base data was found for the query.
    /// Subscribers can trigger a web search fallback.
    /// </summary>
    event EventHandler<string>? NoLocalDataFound;
}
