using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IWebSearchService
{
    /// <summary>Search the web and return top results + abstract text.</summary>
    Task<WebSearchResponse> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>User setting: allow web search fallback.</summary>
    bool IsEnabled { get; set; }
}
