using System.Text.RegularExpressions;

namespace MauMind.App.Services;

/// <summary>
/// Generates follow-up question suggestions from a Q&amp;A pair.
/// Runs entirely on-device — no external API calls.
/// </summary>
public class FollowUpService : IFollowUpService
{
    // ─── Stop words to filter out when extracting key topics ─────────────────
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","is","are","was","were","be","been","being","have","has","had",
        "do","does","did","will","would","could","should","may","might","shall","can",
        "i","you","he","she","it","we","they","me","him","her","us","them",
        "what","which","who","whom","whose","when","where","why","how",
        "that","this","these","those","and","but","or","nor","for","yet","so",
        "in","on","at","by","with","from","to","of","about","as","into","through",
        "during","before","after","above","below","between","under","all","each",
        "if","then","than","because","while","although","though","until","unless",
        "not","no","nor","never","always","just","very","also","too","more","most",
        "your","my","our","their","its","some","any","both","few","many","much",
        "other","same","such","even","only","own","rather","quite","per","via"
    };

    // ─── Question template patterns ───────────────────────────────────────────
    private static readonly string[] ExplainTemplates =
    [
        "Can you explain {0} in more detail?",
        "What does {0} mean exactly?",
        "How does {0} work?",
        "Why is {0} important?",
        "What are examples of {0}?",
    ];

    private static readonly string[] CompareTemplates =
    [
        "What is the difference between {0} and {1}?",
        "How does {0} compare to {1}?",
        "Can you compare {0} and {1}?",
    ];

    private static readonly string[] GeneralTemplates =
    [
        "Can you summarize the key points?",
        "What are the main benefits of this?",
        "How can I apply this in practice?",
        "What should I know next about this topic?",
        "Are there any limitations or drawbacks?",
        "Can you give a real-world example?",
        "What are common mistakes to avoid?",
        "How does this relate to everyday life?",
    ];
    
    // Track recently used questions to avoid duplicates
    private static readonly HashSet<string> _recentQuestions = new(StringComparer.OrdinalIgnoreCase);

    // ─── Public API ───────────────────────────────────────────────────────────

    public List<string> GenerateFollowUps(string userQuery, string aiResponse)
    {
        var followUps = new List<string>();
        var usedQuestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract meaningful topics from the AI response
        var topics = ExtractKeyTopics(aiResponse, maxTopics: 5);

        // Strategy 1: Topic-based "explain more" questions
        if (topics.Count >= 1)
        {
            var t = Capitalize(topics[0]);
            var question = PickTemplate(ExplainTemplates, t);
            if (!usedQuestions.Contains(question))
            {
                usedQuestions.Add(question);
                followUps.Add(question);
            }
        }

        // Strategy 2: Compare two topics
        if (topics.Count >= 2 && followUps.Count < 3)
        {
            var t1 = Capitalize(topics[0]);
            var t2 = Capitalize(topics[1]);
            var question = PickTemplate(CompareTemplates, t1, t2);
            if (!usedQuestions.Contains(question))
            {
                usedQuestions.Add(question);
                followUps.Add(question);
            }
        }

        // Strategy 3: Query morphing — rephrase original question
        if (followUps.Count < 3)
        {
            var morphed = MorphQuestion(userQuery, topics);
            if (!string.IsNullOrEmpty(morphed) && !usedQuestions.Contains(morphed))
            {
                usedQuestions.Add(morphed);
                followUps.Add(morphed);
            }
        }

        // Strategy 4: Fill remaining slots with general templates
        var rng = new Random(userQuery.GetHashCode()); // deterministic per query
        var shuffled = GeneralTemplates.OrderBy(_ => rng.Next()).ToList();
        int genIdx = 0;
        while (followUps.Count < 3 && genIdx < shuffled.Count)
        {
            var q = shuffled[genIdx++];
            if (!usedQuestions.Contains(q))
            {
                usedQuestions.Add(q);
                followUps.Add(q);
            }
        }

        return followUps.Take(3).ToList();
    }

    // ─── Key Topic Extraction ─────────────────────────────────────────────────

    private static List<string> ExtractKeyTopics(string text, int maxTopics)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        // Tokenize: split on non-word chars, lowercase
        var words = Regex.Split(text.ToLower(), @"\W+")
            .Where(w => w.Length >= 4 && !StopWords.Contains(w))
            .ToList();

        // Count word frequency
        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in words)
            freq[w] = freq.TryGetValue(w, out var c) ? c + 1 : 1;

        // Also look for capitalized phrases (proper nouns / key concepts)
        var nounPhrases = Regex.Matches(text, @"\b([A-Z][a-z]{2,}(?:\s+[A-Z][a-z]{2,})*)\b")
            .Select(m => m.Value.ToLower())
            .Where(p => !StopWords.Contains(p))
            .ToList();

        foreach (var p in nounPhrases)
            freq[p] = freq.TryGetValue(p, out var c) ? c + 2 : 2; // boost

        return freq
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .Take(maxTopics)
            .ToList();
    }

    // ─── Question Morphing ────────────────────────────────────────────────────

    private static string MorphQuestion(string query, List<string> topics)
    {
        var lower = query.ToLower().Trim('?', '.', '!', ' ');

        // "What is X" → "What are the benefits of X?"
        var whatIsMatch = Regex.Match(lower, @"^what\s+is\s+(.+)$");
        if (whatIsMatch.Success)
            return $"What are the benefits of {whatIsMatch.Groups[1].Value}?";

        // "How does X work" → "What are the limitations of X?"
        var howMatch = Regex.Match(lower, @"^how\s+does\s+(.+)\s+work");
        if (howMatch.Success)
            return $"What are the limitations of {howMatch.Groups[1].Value}?";

        // "Tell me about X" → "Can you give examples of X?"
        var tellMatch = Regex.Match(lower, @"^(?:tell me about|explain|describe)\s+(.+)$");
        if (tellMatch.Success)
            return $"Can you give examples of {tellMatch.Groups[1].Value}?";

        // Fallback: use first topic if available
        if (topics.Count >= 1)
            return $"What are the practical applications of {Capitalize(topics[0])}?";

        return string.Empty;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string PickTemplate(string[] templates, params string[] args)
    {
        var t = templates[Math.Abs(string.Join("", args).GetHashCode()) % templates.Length];
        return string.Format(t, args);
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
