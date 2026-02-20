namespace MauMind.App.Models;

public class VectorEntry
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public byte[] Embedding { get; set; } = Array.Empty<byte>();
    
    // Navigation property (not stored in DB)
    public Document? Document { get; set; }
}
