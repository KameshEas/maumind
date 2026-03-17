using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IWritingAssistantService
{
    /// <summary>
    /// Analyze text for grammar, style, and clarity issues.
    /// </summary>
    Task<List<WritingSuggestion>> AnalyzeTextAsync(string text);

    /// <summary>
    /// Rewrite text for a specific tone.
    /// </summary>
    Task<string> RewriteForToneAsync(string text, ToneType tone);

    /// <summary>
    /// Rewrite text with advanced options: tone, optional style hints, and length control.
    /// </summary>
    Task<string> RewriteWithOptionsAsync(string text, ToneType tone, string? styleHint = null, LengthType length = LengthType.Medium);

    /// <summary>
    /// Improve clarity of the text.
    /// </summary>
    Task<string> ImproveClarityAsync(string text);

    /// <summary>
    /// Get a complete rewrite of the text.
    /// </summary>
    Task<string> RewriteAsync(string text);
}