using System.Text.Json;
using MauMind.App.Models;

namespace MauMind.App.Services;

public class WritingAssistantService : IWritingAssistantService
{
    private readonly IChatService _chatService;

    public WritingAssistantService(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task<List<WritingSuggestion>> AnalyzeTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
            return new List<WritingSuggestion>();

        var prompt = $@"Analyze the following text for grammar, spelling, style, and clarity issues. Return suggestions as a JSON array.

Text to analyze:
""{text}""

Return ONLY a valid JSON array with this exact format (no markdown, no code blocks):
[
  {{
    ""original"": ""the exact text to replace"",
    ""suggested"": ""the corrected text"",
    ""type"": ""grammar|style|clarity|tone"",
    ""explanation"": ""brief explanation of the issue""
  }}
]

Rules:
- Only include real issues, not false positives
- Type must be exactly: grammar, style, clarity, or tone
- Keep explanations brief (under 10 words)
- If no issues found, return: []
- Focus on significant issues only";

        try
        {
            var responseBuilder = new System.Text.StringBuilder();
            await foreach (var token in _chatService.GetStreamingResponseAsync(prompt))
            {
                responseBuilder.Append(token);
            }

            var response = responseBuilder.ToString().Trim();

            // Clean up response - remove markdown code blocks if present
            if (response.StartsWith("```"))
            {
                var lines = response.Split('\n');
                response = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            var suggestions = JsonSerializer.Deserialize<List<JsonSuggestion>>(response);
            if (suggestions == null || !suggestions.Any())
                return new List<WritingSuggestion>();

            var result = new List<WritingSuggestion>();
            foreach (var s in suggestions)
            {
                var startIndex = text.IndexOf(s.Original, StringComparison.OrdinalIgnoreCase);
                if (startIndex >= 0)
                {
                    result.Add(new WritingSuggestion
                    {
                        OriginalText = s.Original,
                        SuggestedText = s.Suggested,
                        Type = ParseSuggestionType(s.Type),
                        Explanation = s.Explanation,
                        StartIndex = startIndex,
                        EndIndex = startIndex + s.Original.Length
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error analyzing text: {ex.Message}");
            return new List<WritingSuggestion>();
        }
    }

    public async Task<string> RewriteForToneAsync(string text, ToneType tone)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var toneDescription = tone switch
        {
            ToneType.Professional => "professional and business-appropriate",
            ToneType.Casual => "casual and conversational",
            ToneType.Formal => "formal and authoritative",
            ToneType.Academic => "academic and scholarly",
            ToneType.Friendly => "friendly and warm",
            _ => "professional"
        };

        var prompt = $@"Rewrite the following text to be more {toneDescription}. Preserve the core meaning but adjust the tone, word choice, and style.

Original text:
""{text}""

Return ONLY the rewritten text with no explanations or additional commentary.";

        try
        {
            var responseBuilder = new System.Text.StringBuilder();
            await foreach (var token in _chatService.GetStreamingResponseAsync(prompt))
            {
                responseBuilder.Append(token);
            }

            return responseBuilder.ToString().Trim();
        }
        catch
        {
            return text;
        }
    }

    public async Task<string> ImproveClarityAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var prompt = $@"Improve the clarity of the following text. Make it easier to understand while preserving the meaning. Fix any confusing phrasing, run-on sentences, or unclear references.

Original text:
""{text}""

Return ONLY the improved text with no explanations.";

        try
        {
            var responseBuilder = new System.Text.StringBuilder();
            await foreach (var token in _chatService.GetStreamingResponseAsync(prompt))
            {
                responseBuilder.Append(token);
            }

            return responseBuilder.ToString().Trim();
        }
        catch
        {
            return text;
        }
    }

    public async Task<string> RewriteAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var prompt = $@"Rewrite the following text to improve its overall quality. Fix grammar, improve style, enhance clarity, and make it more engaging. Preserve the core meaning.

Original text:
""{text}""

Return ONLY the rewritten text with no explanations.";

        try
        {
            var responseBuilder = new System.Text.StringBuilder();
            await foreach (var token in _chatService.GetStreamingResponseAsync(prompt))
            {
                responseBuilder.Append(token);
            }

            return responseBuilder.ToString().Trim();
        }
        catch
        {
            return text;
        }
    }

    private SuggestionType ParseSuggestionType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "grammar" => SuggestionType.Grammar,
            "style" => SuggestionType.Style,
            "clarity" => SuggestionType.Clarity,
            "tone" => SuggestionType.Tone,
            _ => SuggestionType.Grammar
        };
    }

    // Inner class for JSON deserialization
    private class JsonSuggestion
    {
        public string Original { get; set; } = string.Empty;
        public string Suggested { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }
}