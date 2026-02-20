using MauMind.App.Data;
using MauMind.App.Models;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MauMind.App.Services;

/// <summary>
/// Advanced rule-based chat service with intelligent question answering
/// Uses NLP techniques to understand questions and generate relevant answers
/// </summary>
public class SmartChatService : IChatService, IDisposable
{
    private readonly IVectorStore _vectorStore;
    private readonly DatabaseService _databaseService;
    private bool _isModelLoaded;
    
    public bool IsModelLoaded => _isModelLoaded;
    public event EventHandler<string>? NoLocalDataFound;
    
    public SmartChatService(IVectorStore vectorStore, DatabaseService databaseService)
    {
        _vectorStore = vectorStore;
        _databaseService = databaseService;
    }
    
    public async Task LoadModelAsync(IProgress<int>? progress = null)
    {
        if (_isModelLoaded) return;
        await Task.Run(() =>
        {
            progress?.Report(50);
            _isModelLoaded = true;
            progress?.Report(100);
        });
    }
    
    public async IAsyncEnumerable<string> GetStreamingResponseAsync(
        string userMessage, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_isModelLoaded) await LoadModelAsync();
        
        // Get all documents for context
        var allDocuments = await _databaseService.GetAllDocumentsAsync();
        
        if (allDocuments.Count == 0)
        {
            NoLocalDataFound?.Invoke(this, userMessage);
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }
        
        // Analyze the question and generate a smart answer
        var answer = await AnalyzeAndAnswerAsync(userMessage, allDocuments);
        
        if (string.IsNullOrWhiteSpace(answer))
        {
            NoLocalDataFound?.Invoke(this, userMessage);
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }
        
        // Stream the answer word by word - use StringSplitOptions.RemoveEmptyEntries
        var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length == 0)
        {
            NoLocalDataFound?.Invoke(this, userMessage);
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }
        
        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Delay(3, cancellationToken); // Fast streaming
        }
    }
    
    private async Task<string> AnalyzeAndAnswerAsync(string question, List<Document> documents)
    {
        // Step 1: Understand the question type
        var questionType = ClassifyQuestion(question);
        
        // Step 2: Find relevant content
        var relevantContent = FindRelevantContent(question, documents, questionType);
        
        if (relevantContent.Count == 0)
        {
            return string.Empty;
        }
        
        // Step 3: Generate a direct answer based on question type
        return GenerateSmartAnswer(question, relevantContent, questionType);
    }
    
    private QuestionType ClassifyQuestion(string question)
    {
        var q = question.ToLower().Trim();
        
        // Summary questions
        if (q.Contains("summarize") || q.Contains("summary") || q.Contains("brief"))
            return QuestionType.Summary;
        
        // List questions
        if (q.Contains("list") || q.Contains("name") || q.Contains("what are") || q.Contains("which"))
            return QuestionType.List;
        
        // Definition questions
        if (q.Contains("what is") || q.Contains("what's") || q.Contains("define") || q.Contains("meaning"))
            return QuestionType.Definition;
        
        // How-to questions
        if (q.Contains("how to") || q.Contains("how do") || q.Contains("steps") || q.Contains("process"))
            return QuestionType.HowTo;
        
        // Reason questions
        if (q.Contains("why") || q.Contains("because") || q.Contains("reason"))
            return QuestionType.Reason;
        
        // Yes/No questions
        if (q.Contains("is there") || q.Contains("are there") || q.Contains("can i") || q.Contains("do i"))
            return QuestionType.YesNo;
        
        // Count questions
        if (q.Contains("how many") || q.Contains("count") || q.Contains("number of"))
            return QuestionType.Count;
        
        // Comparison questions
        if (q.Contains("compare") || q.Contains("difference") || q.Contains("versus") || q.Contains("vs "))
            return QuestionType.Comparison;
        
        // Default - informational
        return QuestionType.Informational;
    }
    
    private List<RelevantChunk> FindRelevantContent(string question, List<Document> documents, QuestionType type)
    {
        var questionKeywords = ExtractKeywords(question);
        var chunks = new List<RelevantChunk>();
        
        foreach (var doc in documents)
        {
            // Score based on title match
            var titleScore = CalculateTitleMatch(question, doc.Title);
            
            // Split content into sentences and score each
            var sentences = SplitIntoSentences(doc.Content);
            
            foreach (var sentence in sentences)
            {
                var score = CalculateSentenceScore(question, sentence, questionKeywords, titleScore);
                
                if (score > 0.1) // Minimum threshold
                {
                    chunks.Add(new RelevantChunk
                    {
                        Text = sentence.Trim(),
                        Score = score,
                        DocumentTitle = doc.Title,
                        DocumentId = doc.Id
                    });
                }
            }
        }
        
        // Sort by score and take top results
        return chunks.OrderByDescending(c => c.Score).Take(5).ToList();
    }
    
    private List<string> ExtractKeywords(string text)
    {
        // Remove common words and extract meaningful keywords
        var stopWords = new HashSet<string>
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
        
        var words = text.ToLower()
            .Split(new[] { ' ', ',', '.', '?', '!', '\n', '\r', '\t', ';', ':', '(', ')', '[', ']' }, 
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .ToList();
        
        return words;
    }
    
    private double CalculateTitleMatch(string question, string title)
    {
        var questionLower = question.ToLower();
        var titleLower = title.ToLower();
        
        var titleWords = titleLower.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var matchCount = titleWords.Count(tw => questionLower.Contains(tw));
        
        return matchCount * 0.5; // Title matches are weighted heavily
    }
    
    private double CalculateSentenceScore(string question, string sentence, List<string> keywords, double titleBonus)
    {
        var sentenceLower = sentence.ToLower();
        
        // Count keyword matches
        var keywordMatches = keywords.Count(kw => sentenceLower.Contains(kw));
        
        // Bonus for exact phrase matches
        double phraseMatches = 0;
        var phrases = question.ToLower().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var phrase in phrases.Where(p => p.Length > 4))
        {
            if (sentenceLower.Contains(phrase))
                phraseMatches += 0.3;
        }
        
        // Position bonus (earlier sentences often more important)
        var wordCount = sentence.Split(' ').Length;
        var lengthScore = Math.Min(1.0, 100.0 / Math.Max(wordCount, 10)); // Prefer medium-length sentences
        
        var score = (keywordMatches * 0.2) + phraseMatches + titleBonus + (lengthScore * 0.1);
        
        // Penalize very short or very long sentences
        if (wordCount < 5 || wordCount > 100)
            score *= 0.5;
        
        return score;
    }
    
    private List<string> SplitIntoSentences(string content)
    {
        // Smart sentence splitting
        var sentences = Regex.Split(content, @"(?<=[.!?])\s+(?=[A-Z])")
            .Select(s => s.Trim())
            .Where(s => s.Length > 10 && s.Length < 500)
            .ToList();
        
        // If regex didn't work well, fall back to simple split
        if (sentences.Count < 2)
        {
            sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 10 && s.Length < 500)
                .ToList();
        }
        
        return sentences;
    }
    
    private string GenerateSmartAnswer(string question, List<RelevantChunk> chunks, QuestionType type)
    {
        if (chunks.Count == 0) return string.Empty;
        
        // Get top relevant chunks
        var topChunks = chunks.Take(3).ToList();
        
        switch (type)
        {
            case QuestionType.Summary:
                return GenerateSummaryAnswer(question, topChunks);
            
            case QuestionType.List:
                return GenerateListAnswer(question, topChunks);
            
            case QuestionType.Definition:
                return GenerateDefinitionAnswer(question, topChunks);
            
            case QuestionType.HowTo:
                return GenerateHowToAnswer(question, topChunks);
            
            case QuestionType.Reason:
                return GenerateReasonAnswer(question, topChunks);
            
            case QuestionType.YesNo:
                return GenerateYesNoAnswer(question, topChunks);
            
            case QuestionType.Count:
                return GenerateCountAnswer(question, topChunks);
            
            case QuestionType.Comparison:
                return GenerateComparisonAnswer(question, topChunks);
            
            default:
                return GenerateInformationalAnswer(question, topChunks);
        }
    }
    
    private string GenerateSummaryAnswer(string question, List<RelevantChunk> chunks)
    {
        var mainPoints = chunks.Take(2)
            .Select(c => c.Text)
            .ToList();
        
        return $"Here's a summary based on your documents:\n\n• {string.Join("\n• ", mainPoints)}";
    }
    
    private string GenerateListAnswer(string question, List<RelevantChunk> chunks)
    {
        var items = new HashSet<string>(); // Use HashSet to avoid duplicates
        
        foreach (var chunk in chunks)
        {
            // Extract potential list items
            var lines = chunk.Text.Split(new[] { '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 5 && trimmed.Length < 100)
                {
                    items.Add(trimmed);
                }
            }
        }
        
        if (items.Count > 0)
        {
            return $"Based on your documents:\n\n{string.Join("\n• ", items.Take(5))}";
        }
        
        return $"Here's what I found related to your question:\n\n{string.Join(" | ", chunks.Take(2).Select(c => c.Text))}";
    }
    
    private string GenerateDefinitionAnswer(string question, List<RelevantChunk> chunks)
    {
        // Find the chunk with definition-like content
        var definition = chunks.FirstOrDefault(c => 
            c.Text.ToLower().Contains("is defined as") ||
            c.Text.ToLower().Contains("means") ||
            c.Text.ToLower().Contains("refers to") ||
            c.Text.ToLower().StartsWith("it is"));
        
        if (definition != null)
        {
            return $"Based on the definition: {definition.Text}";
        }
        
        return $"From your documents: {chunks[0].Text}";
    }
    
    private string GenerateHowToAnswer(string question, List<RelevantChunk> chunks)
    {
        var steps = new List<string>();
        
        foreach (var chunk in chunks)
        {
            // Look for action words
            var sentences = chunk.Text.Split(new[] { '.', '!' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var sentence in sentences)
            {
                var lower = sentence.ToLower();
                if (lower.Contains("first") || lower.Contains("step") || 
                    lower.Contains("then") || lower.Contains("next") ||
                    lower.StartsWith("to ") || lower.StartsWith("you can"))
                {
                    steps.Add(sentence.Trim());
                }
            }
        }
        
        if (steps.Count > 0)
        {
            return $"Here's how:\n\n{string.Join("\n", steps.Take(3))}";
        }
        
        return $"Based on your documents:\n\n{chunks[0].Text}";
    }
    
    private string GenerateReasonAnswer(string question, List<RelevantChunk> chunks)
    {
        var reasons = chunks
            .Where(c => c.Text.ToLower().Contains("because") || 
                       c.Text.ToLower().Contains("reason") ||
                       c.Text.ToLower().Contains("due to") ||
                       c.Text.ToLower().Contains("since"))
            .Take(2)
            .Select(c => c.Text)
            .ToList();
        
        if (reasons.Count > 0)
        {
            return $"The reason is:\n\n{string.Join("\n\n", reasons)}";
        }
        
        return $"Based on your documents:\n\n{chunks[0].Text}";
    }
    
    private string GenerateYesNoAnswer(string question, List<RelevantChunk> chunks)
    {
        // Look for clear positive/negative indicators
        var hasPositive = chunks.Any(c => 
            c.Text.ToLower().Contains("yes") || 
            c.Text.ToLower().Contains("can") ||
            c.Text.ToLower().Contains("able to") ||
            c.Text.ToLower().Contains("possible"));
        
        var hasNegative = chunks.Any(c => 
            c.Text.ToLower().Contains("no ") || 
            c.Text.ToLower().Contains("cannot") ||
            c.Text.ToLower().Contains("not possible") ||
            c.Text.ToLower().Contains("unable"));
        
        if (hasPositive && !hasNegative)
            return "Yes, based on your documents.";
        
        if (hasNegative && !hasPositive)
            return "No, based on your documents.";
        
        // Neutral answer
        return $"I found relevant information: {chunks[0].Text}";
    }
    
    private string GenerateCountAnswer(string question, List<RelevantChunk> chunks)
    {
        // Try to find numbers in the text
        var numbers = new List<string>();
        
        foreach (var chunk in chunks)
        {
            var matches = Regex.Matches(chunk.Text, @"\d+");
            foreach (Match match in matches)
            {
                numbers.Add(match.Value);
            }
        }
        
        if (numbers.Count > 0)
        {
            var uniqueNumbers = numbers.Distinct().Take(5);
            return $"I found these numbers in your documents: {string.Join(", ", uniqueNumbers)}";
        }
        
        return $"I found {chunks.Count} relevant sections in your documents.";
    }
    
    private string GenerateComparisonAnswer(string question, List<RelevantChunk> chunks)
    {
        var comparisons = new List<string>();
        
        foreach (var chunk in chunks)
        {
            var lower = chunk.Text.ToLower();
            if (lower.Contains("vs ") || lower.Contains("versus") ||
                lower.Contains("compared to") || lower.Contains("different from") ||
                lower.Contains("while ") || lower.Contains("whereas"))
            {
                comparisons.Add(chunk.Text);
            }
        }
        
        if (comparisons.Count > 0)
        {
            return $"Here's the comparison:\n\n{string.Join("\n\n", comparisons.Take(2))}";
        }
        
        return $"Based on your documents:\n\n{chunks[0].Text}";
    }
    
    private string GenerateInformationalAnswer(string question, List<RelevantChunk> chunks)
    {
        // Find the most relevant sentence
        var bestMatch = chunks.FirstOrDefault();
        
        if (bestMatch != null)
        {
            // Extract the most relevant part
            var sentences = bestMatch.Text.Split(new[] { '.', '!' }, StringSplitOptions.RemoveEmptyEntries);
            
            var questionKeywords = question.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .ToList();
            
            var relevantSentence = sentences.FirstOrDefault(s => 
                questionKeywords.Any(kw => s.ToLower().Contains(kw)));
            
            if (!string.IsNullOrWhiteSpace(relevantSentence))
            {
                return relevantSentence.Trim() + ".";
            }
            
            return bestMatch.Text;
        }
        
        return string.Empty;
    }
    
    public async Task<List<ChatMessage>> GetChatHistoryAsync() => 
        await _databaseService.GetChatMessagesAsync();
    
    public async Task SaveMessageAsync(ChatMessage message) =>
        await _databaseService.InsertChatMessageAsync(message);
    
    public async Task ClearHistoryAsync() =>
        await _databaseService.ClearChatMessagesAsync();
    
    public void Dispose() { }
}

public enum QuestionType
{
    Summary,
    List,
    Definition,
    HowTo,
    Reason,
    YesNo,
    Count,
    Comparison,
    Informational
}

public class RelevantChunk
{
    public string Text { get; set; } = "";
    public double Score { get; set; }
    public string DocumentTitle { get; set; } = "";
    public int DocumentId { get; set; }
}
