namespace MauMind.App.Models;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocumentSourceType SourceType { get; set; }
    public string? FilePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Smart Summarization
    public string? Summary { get; set; }
    public DateTime? SummarizedAt { get; set; }

    // Folder/Collection System
    public int? FolderId { get; set; }
    public Folder? Folder { get; set; }
}

public enum DocumentSourceType
{
    Note,
    PDF,
    Log,
    Scanned,
    Image
}
