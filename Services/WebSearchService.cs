using MauMind.App.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace MauMind.App.Services;

/// <summary>
/// Web search using DuckDuckGo Instant Answer API.
/// Free, no API key required, privacy-first.
/// </summary>
public class WebSearchService : IWebSearchService, IDisposable
{
    private const string PrefKey  = "web_search_enabled";
    private const string DdgUrl   = "https://api.duckduckgo.com/";
    private const string HtmlUrl  = "https://html.duckduckgo.com/html/";

    private readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "MauMind/1.0 (privacy-first AI assistant)" }
        }
    };

    public bool IsEnabled
    {
        get => Preferences.Get(PrefKey, true);
        set => Preferences.Set(PrefKey, value);
    }

    // ─── Search ────────────────────────────────────────────────────────────────

    public async Task<WebSearchResponse> SearchAsync(
        string query,
        CancellationToken ct = default)
    {
        var response = new WebSearchResponse { Query = query };

        try
        {
            // 1. Try DuckDuckGo Instant Answer API first (JSON)
            var ddgResult = await TryDdgInstantAnswer(query, ct);
            if (ddgResult.IsSuccess && ddgResult.Results.Count > 0)
                return ddgResult;

            // 2. If no instant answer, try HTML scraping fallback
            var htmlResult = await TryDdgHtmlSearch(query, ct);
            return htmlResult;
        }
        catch (TaskCanceledException)
        {
            response.IsSuccess     = false;
            response.ErrorMessage  = "Search timed out. Check your internet connection.";
            return response;
        }
        catch (Exception ex)
        {
            response.IsSuccess    = false;
            response.ErrorMessage = $"Search failed: {ex.Message}";
            return response;
        }
    }

    // ─── DuckDuckGo Instant Answer ─────────────────────────────────────────────

    private async Task<WebSearchResponse> TryDdgInstantAnswer(string query, CancellationToken ct)
    {
        var encoded = HttpUtility.UrlEncode(query);
        var url     = $"{DdgUrl}?q={encoded}&format=json&no_html=1&no_redirect=1&skip_disambig=1";

        var json     = await _http.GetStringAsync(url, ct);
        var doc      = JsonDocument.Parse(json);
        var root     = doc.RootElement;

        var response = new WebSearchResponse { Query = query };

        // Abstract text (best for factual questions)
        var abstractText = root.TryGetProperty("AbstractText", out var at) ? at.GetString() : null;
        var abstractUrl  = root.TryGetProperty("AbstractURL",  out var au) ? au.GetString() : null;
        var abstractSrc  = root.TryGetProperty("AbstractSource", out var asrc) ? asrc.GetString() : null;

        if (!string.IsNullOrWhiteSpace(abstractText))
        {
            response.AbstractText = abstractText;
            response.Results.Add(new WebSearchResult
            {
                Title   = root.TryGetProperty("Heading", out var h) ? h.GetString() ?? query : query,
                Snippet = abstractText,
                Url     = abstractUrl ?? string.Empty,
                Source  = abstractSrc ?? "DuckDuckGo",
            });
        }

        // Related topics
        if (root.TryGetProperty("RelatedTopics", out var topics))
        {
            foreach (var topic in topics.EnumerateArray().Take(3))
            {
                var text = topic.TryGetProperty("Text", out var t) ? t.GetString() : null;
                var link = topic.TryGetProperty("FirstURL", out var fu) ? fu.GetString() : null;

                if (!string.IsNullOrWhiteSpace(text) && response.Results.Count < 4)
                {
                    response.Results.Add(new WebSearchResult
                    {
                        Title   = topic.TryGetProperty("Name", out var n) ? n.GetString() ?? "Result" : "Result",
                        Snippet = text,
                        Url     = link ?? string.Empty,
                        Source  = "DuckDuckGo",
                    });
                }
            }
        }

        response.IsSuccess = response.Results.Count > 0;
        return response;
    }

    // ─── DuckDuckGo HTML Fallback ──────────────────────────────────────────────

    private async Task<WebSearchResponse> TryDdgHtmlSearch(string query, CancellationToken ct)
    {
        var response = new WebSearchResponse { Query = query };

        var content  = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("q", query),
        });

        var httpResp = await _http.PostAsync(HtmlUrl, content, ct);
        var html     = await httpResp.Content.ReadAsStringAsync(ct);

        // Simple regex-based snippet extraction
        var snippets = ExtractSnippetsFromHtml(html);

        foreach (var s in snippets.Take(3))
        {
            response.Results.Add(new WebSearchResult
            {
                Title   = s.title,
                Snippet = s.snippet,
                Url     = s.url,
                Source  = "DuckDuckGo",
            });
        }

        response.IsSuccess = response.Results.Count > 0;
        if (!response.IsSuccess)
            response.ErrorMessage = "No results found for your query.";

        return response;
    }

    private static List<(string title, string snippet, string url)> ExtractSnippetsFromHtml(string html)
    {
        var results = new List<(string, string, string)>();

        // Find result blocks by class
        int pos = 0;
        while (results.Count < 4)
        {
            var resultStart = html.IndexOf("class=\"result__body\"", pos, StringComparison.OrdinalIgnoreCase);
            if (resultStart < 0) break;

            // Extract title
            var titleStart = html.IndexOf("class=\"result__a\"", resultStart, StringComparison.OrdinalIgnoreCase);
            var title = "";
            if (titleStart >= 0)
            {
                var ts = html.IndexOf('>', titleStart) + 1;
                var te = html.IndexOf('<', ts);
                if (ts > 0 && te > ts)
                    title = html[ts..te].Trim();
            }

            // Extract snippet
            var snippetStart = html.IndexOf("class=\"result__snippet\"", resultStart, StringComparison.OrdinalIgnoreCase);
            var snippet = "";
            if (snippetStart >= 0)
            {
                var ss = html.IndexOf('>', snippetStart) + 1;
                var se = html.IndexOf("</a>", ss, StringComparison.OrdinalIgnoreCase);
                if (ss > 0 && se > ss)
                {
                    snippet = html[ss..se];
                    // Strip remaining tags
                    snippet = System.Text.RegularExpressions.Regex.Replace(snippet, "<[^>]+>", "").Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(snippet))
                results.Add((title, snippet, "https://duckduckgo.com"));

            pos = resultStart + 1;
        }

        return results;
    }

    // ─── Format results for context ───────────────────────────────────────────

    public static string FormatResultsAsContext(WebSearchResponse response)
    {
        if (!response.IsSuccess || response.Results.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Web search results for: \"{response.Query}\"]");
        sb.AppendLine();

        foreach (var (r, i) in response.Results.Select((r, i) => (r, i + 1)))
        {
            sb.AppendLine($"{i}. {r.Title}");
            sb.AppendLine(r.Snippet);
            if (!string.IsNullOrEmpty(r.Url))
                sb.AppendLine($"   Source: {r.Url}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public void Dispose() => _http.Dispose();
}
