using MauMind.App.Data;
using MauMind.App.Models;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MauMind.App.Services;

/// <summary>
/// Hybrid Chat Service that combines semantic search (vector similarity) 
/// and keyword-based search for better accuracy and coverage.
/// 
/// Strategy:
/// 1. First try vector search for semantic similarity
/// 2. If no good semantic match, try keyword-based search
/// 3. Combine and rank results from both approaches
/// 4. Generate intelligent answer based on best match
/// </summary>
public class HybridChatService : IChatService, IDisposable
{
    private readonly IVectorStore _vectorStore;
    private readonly DatabaseService _databaseService;
    private bool _isModelLoaded;
    
    public bool IsModelLoaded => _isModelLoaded;
    public event EventHandler<string>? NoLocalDataFound;
    
    // Configuration
    private const float SemanticThreshold = 0.2f;  // Lowered for more semantic matches
    private const int TopK = 8;  // Get more results for hybrid scoring
    private const int MaxAnswerLength = 500;  // Limit answer length
    
    public HybridChatService(IVectorStore vectorStore, DatabaseService databaseService)
    {
        _vectorStore = vectorStore;
        _databaseService = databaseService;
    }
    
    public async Task LoadModelAsync(IProgress<int>? progress = null)
    {
        if (_isModelLoaded) return;
        
        await Task.Run(() =>
        {
            progress?.Report(30);
            
            // Initialize vector store (loads embedding model)
            // This is handled by VectorStore.InitializeAsync() called from ChatViewModel
            
            progress?.Report(100);
            _isModelLoaded = true;
        });
    }
    
    public async IAsyncEnumerable<string> GetStreamingResponseAsync(
        string userMessage, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_isModelLoaded)
        {
            await LoadModelAsync();
        }
        
        // Get all documents for keyword fallback
        var allDocuments = new List<Document>();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            allDocuments = await _databaseService.GetAllDocumentsAsync().WaitAsync(cts.Token);
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine("Document fetch timed out");
        }
        
        if (allDocuments.Count == 0)
        {
            NoLocalDataFound?.Invoke(this, userMessage);
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }
        
        // Try semantic search first
        var semanticResults = await GetSemanticResultsAsync(userMessage);
        
        // Try keyword search
        var keywordResults = GetKeywordResults(userMessage, allDocuments);
        
        // Combine and rank results
        var hybridResults = CombineResults(semanticResults, keywordResults);
        
        if (hybridResults.Count == 0)
        {
            NoLocalDataFound?.Invoke(this, userMessage);
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }
        
        // Generate the best answer
        var answer = GenerateHybridAnswer(userMessage, hybridResults, allDocuments);
        
        if (string.IsNullOrWhiteSpace(answer))
        {
            NoLocalDataFound?.Invoke(this, userMessage);
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }
        
        // Stream the answer by phrases (faster than word-by-word)
        var phrases = SplitIntoPhrases(answer);
        
        if (phrases.Length == 0)
        {
            yield return "I found relevant information in your documents.";
            yield break;
        }
        
        foreach (var phrase in phrases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return phrase + " ";
            await Task.Delay(8, cancellationToken); // Fast streaming
        }
    }
    
    /// <summary>
    /// Get semantic search results using vector similarity
    /// </summary>
    private async Task<List<HybridResult>> GetSemanticResultsAsync(string query)
    {
        var results = new List<HybridResult>();
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var vectorResults = await _vectorStore.SearchAsync(query, TopK).WaitAsync(cts.Token);
            
            foreach (var (entry, score) in vectorResults)
            {
                if (score >= SemanticThreshold)
                {
                    results.Add(new HybridResult
                    {
                        Text = entry.ChunkText,
                        Score = score,
                        Source = ResultSource.Semantic,
                        DocumentId = entry.DocumentId
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Semantic search error: {ex.Message}");
        }
        
        return results;
    }
    
    /// <summary>
    /// Get keyword-based search results
    /// </summary>
    private List<HybridResult> GetKeywordResults(string query, List<Document> documents)
    {
        var results = new List<HybridResult>();
        var keywords = ExtractKeywords(query);
        
        if (keywords.Count == 0) return results;
        
        foreach (var doc in documents)
        {
            var sentences = SplitIntoSentences(doc.Content);
            
            foreach (var sentence in sentences)
            {
                var keywordScore = CalculateKeywordScore(sentence, keywords);
                
                if (keywordScore > 0)
                {
                    results.Add(new HybridResult
                    {
                        Text = sentence.Trim(),
                        Score = (float)(keywordScore * 0.8), // Weight keyword results slightly lower
                        Source = ResultSource.Keyword,
                        DocumentId = doc.Id,
                        DocumentTitle = doc.Title
                    });
                }
            }
        }
        
        // Take top keyword results
        return results.OrderByDescending(r => r.Score).Take(TopK).ToList();
    }
    
    /// <summary>
    /// Combine semantic and keyword results, removing duplicates
    /// </summary>
    private List<HybridResult> CombineResults(
        List<HybridResult> semanticResults, 
        List<HybridResult> keywordResults)
    {
        var combined = new List<HybridResult>();
        var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // First add semantic results (higher priority)
        foreach (var result in semanticResults.OrderByDescending(r => r.Score))
        {
            var normalized = NormalizeText(result.Text);
            if (!seenTexts.Contains(normalized) && result.Score > 0.1f)
            {
                seenTexts.Add(normalized);
                combined.Add(result);
            }
        }
        
        // Then add keyword results that aren't duplicates
        foreach (var result in keywordResults.OrderByDescending(r => r.Score))
        {
            var normalized = NormalizeText(result.Text);
            if (!seenTexts.Contains(normalized) && result.Score > 0.15f)
            {
                seenTexts.Add(normalized);
                combined.Add(result);
            }
        }
        
        return combined.Take(10).ToList();
    }
    
    /// <summary>
    /// Generate a hybrid answer based on the best matching results
    /// </summary>
    private string GenerateHybridAnswer(
        string query, 
        List<HybridResult> results,
        List<Document> documents)
    {
        if (results.Count == 0)
            return string.Empty;
        
        // Analyze the question type
        var questionType = ClassifyQuestion(query);
        
        // Get the top results
        var topResults = results.Take(3).ToList();
        
        // Generate answer based on question type
        return questionType switch
        {
            QuestionType.Summary => GenerateSummaryAnswer(query, topResults),
            QuestionType.List => GenerateListAnswer(query, topResults),
            QuestionType.Definition => GenerateDefinitionAnswer(query, topResults),
            QuestionType.HowTo => GenerateHowToAnswer(query, topResults),
            QuestionType.Reason => GenerateReasonAnswer(query, topResults),
            QuestionType.YesNo => GenerateYesNoAnswer(query, topResults),
            QuestionType.Count => GenerateCountAnswer(query, topResults),
            QuestionType.Comparison => GenerateComparisonAnswer(query, topResults),
            _ => GenerateInformationalAnswer(query, topResults)
        };
    }
    
    private QuestionType ClassifyQuestion(string question)
    {
        var q = question.ToLower().Trim();
        
        if (q.Contains("summarize") || q.Contains("summary") || q.Contains("brief"))
            return QuestionType.Summary;
        
        if (q.Contains("list") || q.Contains("name") || q.Contains("what are") || q.Contains("which"))
            return QuestionType.List;
        
        if (q.Contains("what is") || q.Contains("what's") || q.Contains("define"))
            return QuestionType.Definition;
        
        if (q.Contains("how to") || q.Contains("how do") || q.Contains("steps"))
            return QuestionType.HowTo;
        
        if (q.Contains("why") || q.Contains("because") || q.Contains("reason"))
            return QuestionType.Reason;
        
        if (q.Contains("is there") || q.Contains("are there") || q.Contains("can i"))
            return QuestionType.YesNo;
        
        if (q.Contains("how many") || q.Contains("count") || q.Contains("number of"))
            return QuestionType.Count;
        
        if (q.Contains("compare") || q.Contains("difference") || q.Contains("versus") || q.Contains("vs "))
            return QuestionType.Comparison;
        
        return QuestionType.Informational;
    }
    
    private List<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "must", "shall", "can", "need", "dare",
            "to", "of", "in", "for", "on", "with", "at", "by", "from", "as",
            "into", "through", "during", "before", "after", "above", "below",
            "and", "but", "or", "nor", "so", "yet", "both", "either", "neither",
            "not", "only", "just", "also", "very", "too", "quite", "rather",
            "what", "which", "who", "whom", "whose", "where", "when", "why", "how",
            "this", "that", "these", "those", "i", "you", "he", "she", "it", "we",
            "they", "my", "your", "his", "her", "its", "our", "their"
        };
        
        return text.ToLower()
            .Split(new[] { ' ', ',', '.', '?', '!', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .ToList();
    }
    
    private double CalculateKeywordScore(string sentence, List<string> keywords)
    {
        var sentenceLower = sentence.ToLower();
        int matchCount = 0;
        
        foreach (var keyword in keywords)
        {
            if (sentenceLower.Contains(keyword))
                matchCount++;
        }
        
        // Score based on keyword density
        return keywords.Count > 0 ? (double)matchCount / keywords.Count : 0;
    }
    
    private string[] SplitIntoPhrases(string text)
    {
        // Split by clauses for faster streaming
        return text.Split(
            new[] { ". ", "! ", "? ", "; ", ", " },
            StringSplitOptions.RemoveEmptyEntries
        );
    }
    
    private string[] SplitIntoSentences(string content)
    {
        var sentences = Regex.Split(content, @"(?<=[.!?])\s+(?=[A-Z])")
            .Select(s => s.Trim())
            .Where(s => s.Length > 10 && s.Length < 300)
            .ToList();
        
        if (sentences.Count < 2)
        {
            sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 10 && s.Length < 300)
                .ToList();
        }
        
        return sentences.ToArray();
    }
    
    private string NormalizeText(string text)
    {
        return Regex.Replace(text.ToLower(), @"\s+", " ").Trim();
    }
    
    // Answer generation methods
    private string GenerateSummaryAnswer(string question, List<HybridResult> results)
    {
        var points = results.Take(2).Select(r => r.Text).ToList();
        return $"Based on your documents:\n\n• {string.Join("\n• ", points)}";
    }
    
    private string GenerateListAnswer(string question, List<HybridResult> results)
    {
        var items = results
            .SelectMany(r => r.Text.Split(new[] { '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(s => s.Trim())
            .Where(s => s.Length > 5 && s.Length < 100)
            .Distinct()
            .Take(5)
            .ToList();
        
        if (items.Count > 0)
            return $"Here are the key items:\n\n• {string.Join("\n• ", items)}";
        
        return $"Here's what I found:\n\n{results[0].Text}";
    }
    
    private string GenerateDefinitionAnswer(string question, List<HybridResult> results)
    {
        var definition = results.FirstOrDefault(r => 
            r.Text.ToLower().Contains("is defined") ||
            r.Text.ToLower().Contains("means") ||
            r.Text.ToLower().Contains("refers to"));
        
        return definition?.Text ?? results[0].Text;
    }
    
    private string GenerateHowToAnswer(string question, List<HybridResult> results)
    {
        var steps = results
            .SelectMany(r => r.Text.Split(new[] { '.', '!' }, StringSplitOptions.RemoveEmptyEntries))
            .Where(s => s.ToLower().Contains("first") || s.ToLower().Contains("step") ||
                       s.ToLower().Contains("then") || s.ToLower().Contains("next") ||
                       s.ToLower().StartsWith("to "))
            .Take(3)
            .ToList();
        
        if (steps.Count > 0)
            return $"Here's how:\n\n{string.Join("\n", steps)}";
        
        return results[0].Text;
    }
    
    private string GenerateReasonAnswer(string question, List<HybridResult> results)
    {
        var reasons = results
            .Where(r => r.Text.ToLower().Contains("because") ||
                       r.Text.ToLower().Contains("reason") ||
                       r.Text.ToLower().Contains("due to"))
            .Take(2)
            .Select(r => r.Text)
            .ToList();
        
        if (reasons.Count > 0)
            return $"The reason is:\n\n{string.Join("\n\n", reasons)}";
        
        return results[0].Text;
    }
    
    private string GenerateYesNoAnswer(string question, List<HybridResult> results)
    {
        var hasPositive = results.Any(r => 
            r.Text.ToLower().Contains("yes") || 
            r.Text.ToLower().Contains("can") ||
            r.Text.ToLower().Contains("possible"));
        
        var hasNegative = results.Any(r => 
            r.Text.ToLower().Contains("no ") || 
            r.Text.ToLower().Contains("cannot") ||
            r.Text.ToLower().Contains("not possible"));
        
        if (hasPositive && !hasNegative)
            return "Yes, based on your documents.";
        
        if (hasNegative && !hasPositive)
            return "No, based on your documents.";
        
        return results[0].Text;
    }
    
    private string GenerateCountAnswer(string question, List<HybridResult> results)
    {
        var numbers = results
            .SelectMany(r => Regex.Matches(r.Text, @"\d+").Cast<Match>())
            .Select(m => m.Value)
            .Distinct()
            .Take(5)
            .ToList();
        
        if (numbers.Count > 0)
            return $"I found these numbers: {string.Join(", ", numbers)}";
        
        return $"I found {results.Count} relevant sections.";
    }
    
    private string GenerateComparisonAnswer(string question, List<HybridResult> results)
    {
        var comparisons = results
            .Where(r => r.Text.ToLower().Contains("vs ") ||
                       r.Text.ToLower().Contains("versus") ||
                       r.Text.ToLower().Contains("compared"))
            .Take(2)
            .Select(r => r.Text)
            .ToList();
        
        if (comparisons.Count > 0)
            return $"Here's the comparison:\n\n{string.Join("\n\n", comparisons)}";
        
        return results[0].Text;
    }
    
    private string GenerateInformationalAnswer(string question, List<HybridResult> results)
    {
        var best = results.FirstOrDefault();
        if (best == null) return string.Empty;
        
        // Try to find the most relevant sentence
        var keywords = ExtractKeywords(question);
        var sentences = best.Text.Split(new[] { '.', '!' }, StringSplitOptions.RemoveEmptyEntries);
        
        var relevantSentence = sentences.FirstOrDefault(s =>
            keywords.Any(kw => s.ToLower().Contains(kw)));
        
        return relevantSentence?.Trim() ?? best.Text;
    }
    
    public async Task<List<ChatMessage>> GetChatHistoryAsync() => 
        await _databaseService.GetChatMessagesAsync();
    
    public async Task SaveMessageAsync(ChatMessage message) =>
        await _databaseService.InsertChatMessageAsync(message);
    
    public async Task ClearHistoryAsync() =>
        await _databaseService.ClearChatMessagesAsync();
    
    public void Dispose() { }
}

public enum ResultSource
{
    Semantic,
    Keyword,
    Hybrid
}

public class HybridResult
{
    public string Text { get; set; } = "";
    public float Score { get; set; }
    public ResultSource Source { get; set; }
    public int DocumentId { get; set; }
    public string DocumentTitle { get; set; } = "";
}
