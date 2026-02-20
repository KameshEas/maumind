namespace MauMind.App.Models;

public class WritingSuggestion
{
    public string OriginalText { get; set; } = string.Empty;
    public string SuggestedText { get; set; } = string.Empty;
    public SuggestionType Type { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public bool IsAccepted { get; set; }
    public bool IsIgnored { get; set; }
}

public enum SuggestionType
{
    Grammar,      // Spelling, verb tense, subject-verb agreement
    Style,        // Word choice, repetition, passive voice
    Clarity,      // Run-on sentences, unclear phrasing
    Tone          // Professional, casual, formal
}

public enum ToneType
{
    Professional,
    Casual,
    Formal,
    Academic,
    Friendly
}