using System;

namespace MauMind.App.Models;

public class Conversation
{
    public int Id { get; set; }
    public string Title { get; set; } = "Untitled";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; }
}
