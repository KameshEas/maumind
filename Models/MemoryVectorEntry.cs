namespace MauMind.App.Models;

public class MemoryVectorEntry
{
    public int Id { get; set; }
    public int MemoryId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public byte[] Embedding { get; set; } = Array.Empty<byte>();
}
