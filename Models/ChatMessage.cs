using CommunityToolkit.Mvvm.ComponentModel;

namespace MauMind.App.Models;

public class ChatMessage : ObservableObject
{
    public int Id { get; set; }

    // Conversation this message belongs to (0 = default/global)
    public int ConversationId { get; set; }

    private string _content = string.Empty;
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
