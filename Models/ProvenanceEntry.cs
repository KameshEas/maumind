namespace MauMind.App.Models;

public class ProvenanceEntry
{
    public int DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public float Score { get; set; }
    public string Source { get; set; } = string.Empty;
}
