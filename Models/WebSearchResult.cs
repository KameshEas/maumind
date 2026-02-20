namespace MauMind.App.Models;

public class WebSearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public class WebSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public List<WebSearchResult> Results { get; set; } = new();
    public string AbstractText { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
