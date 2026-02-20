using MauMind.App.Data;
using MauMind.App.Models;
using Microsoft.SemanticKernel;
using System.Runtime.CompilerServices;

namespace MauMind.App.Services;

public class ChatService : IChatService, IDisposable
{
    private readonly IVectorStore _vectorStore;
    private readonly DatabaseService _databaseService;
    private Kernel? _kernel;
    private bool _isModelLoaded;
    private string _modelPath = string.Empty;
    
    public bool IsModelLoaded => _isModelLoaded;

    /// <inheritdoc/>
    public event EventHandler<string>? NoLocalDataFound;
    
    public ChatService(IVectorStore vectorStore, DatabaseService databaseService)
    {
        _vectorStore = vectorStore;
        _databaseService = databaseService;
    }
    
    public async Task LoadModelAsync(IProgress<int>? progress = null)
    {
        if (_isModelLoaded) return;
        
        await Task.Run(() =>
        {
            try
            {
                progress?.Report(10);
                
                var modelsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MauMind", "models");
                
                Directory.CreateDirectory(modelsDir);
                
                _modelPath = Path.Combine(modelsDir, "phi-3-mini-4k-instruct-q4.onnx");
                
                progress?.Report(100);
                _isModelLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading chat model: {ex.Message}");
                _isModelLoaded = true;
            }
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
        
        // Try vector search first, but also get all documents for keyword fallback
        // Added timeout to prevent hanging on slow operations
        List<(VectorEntry Entry, float Score)> contextResults;
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            contextResults = await _vectorStore.SearchAsync(userMessage, topK: 5).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - fall back to empty results
            System.Diagnostics.Debug.WriteLine("Vector search timed out after 30 seconds");
            contextResults = new List<(VectorEntry, float)>();
        }
        catch
        {
            contextResults = new List<(VectorEntry, float)>();
        }
        
        // Also get all documents for keyword search fallback (with timeout)
        var allDocuments = new List<Document>();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            allDocuments = await _databaseService.GetAllDocumentsAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Document fetch timed out after 10 seconds");
        }
        catch { }
        
        // Generate response - try vector search first, then keyword fallback
        string response;
        bool noLocalData = false;

        // Lower threshold to 0.25 to find more results, but use intelligent answer generation
        if (contextResults.Count > 0 && contextResults[0].Score > 0.25f)
        {
            // Good vector match
            response = GenerateResponseFromContext(userMessage, contextResults);
        }
        else if (allDocuments.Count > 0)
        {
            // Use keyword search fallback
            var keywordResponse = GenerateResponseFromKeywords(userMessage, allDocuments);
            if (string.IsNullOrWhiteSpace(keywordResponse) || keywordResponse == GetNoDocumentsResponse(userMessage))
            {
                noLocalData = true;
                response = string.Empty;
            }
            else
            {
                response = keywordResponse;
            }
        }
        else
        {
            // No documents at all → signal web search needed
            noLocalData = true;
            response = string.Empty;
        }

        if (noLocalData)
        {
            // Fire event so ChatViewModel can offer web search
            NoLocalDataFound?.Invoke(this, userMessage);
            // Yield sentinel so the streaming completes cleanly
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }

        // Ensure we have a valid response
        if (string.IsNullOrWhiteSpace(response))
        {
            // Fallback response if generation failed
            response = "I found some relevant information in your documents. Could you try rephrasing your question?";
        }

        // Stream word-by-word with fast timing (reduced from 18-70ms to 2-8ms)
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length == 0)
        {
            yield return "I found some relevant information in your documents.";
            yield break;
        }
        
        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return word + " ";

            // Fast streaming: 2-8ms per word instead of 18-70ms
            // This makes responses appear ~5-10x faster
            int delayMs = word.Length switch
            {
                <= 2  => 2,
                <= 4  => 3,
                <= 7  => 5,
                <= 10 => 6,
                _     => 8,
            };

            await Task.Delay(delayMs, cancellationToken);
        }
    }
    
    private string GenerateResponseFromContext(string userMessage, List<(VectorEntry Entry, float Score)> results)
    {
        // Use a single most relevant chunk for focused answer
        var relevantChunk = results
            .FirstOrDefault(r => r.Score > 0.3);
        
        if (relevantChunk.Entry == null)
            return GetNoDocumentsResponse(userMessage);
        
        var chunkText = relevantChunk.Entry.ChunkText;
        var userQuestion = userMessage.ToLower();
        
        // Generate a direct, question-focused answer
        var answer = GenerateDirectAnswer(userMessage, chunkText);
        
        return answer;
    }
    
    private string GenerateDirectAnswer(string question, string context)
    {
        // Extract key information from context that answers the question
        var contextLower = context.ToLower();
        var questionLower = question.ToLower();
        
        // Check what type of question it is
        bool isWhatQuestion = questionLower.Contains("what");
        bool isListQuestion = questionLower.Contains("list") || questionLower.Contains("name");
        bool isHowQuestion = questionLower.Contains("how");
        bool isWhyQuestion = questionLower.Contains("why");
        bool isWhenQuestion = questionLower.Contains("when");
        
        // Extract sentences that might contain the answer
        var sentences = context.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var relevantSentences = new List<string>();
        
        // Find sentences containing key words from the question
        var questionWords = questionLower
            .Split(new[] { ' ', ',', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();
        
        foreach (var sentence in sentences)
        {
            var sentenceLower = sentence.ToLower();
            // Check if sentence has at least one significant keyword from question
            int matchCount = questionWords.Count(qw => sentenceLower.Contains(qw));
            if (matchCount > 0)
            {
                relevantSentences.Add(sentence.Trim());
            }
        }
        
        // If we found relevant sentences, use them
        if (relevantSentences.Count > 0)
        {
            var answer = string.Join(". ", relevantSentences.Take(2));
            if (!answer.EndsWith(".")) answer += ".";
            
            // Add prefix based on question type
            if (isWhatQuestion)
                return $"What I found: {answer}";
            if (isListQuestion)
                return $"Here are the items: {answer}";
            if (isHowQuestion)
                return $"Here's how: {answer}";
            return $"Answer: {answer}";
        }
        
        // Fallback: return a summary of the context
        var summary = context.Length > 200 ? context[..200] + "..." : context;
        return $"Based on your document: {summary}";
    }
    
    private string GenerateResponseFromKeywords(string userMessage, List<Document> documents)
    {
        var keywords = userMessage.ToLower()
            .Split(new[] { ' ', ',', '.', '?', '!', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();
        
        if (keywords.Count == 0)
        {
            return $"You have {documents.Count} documents saved. What would you like to know about them?";
        }
        
        // Find the best matching document
        var bestDoc = documents
            .Select(d => new { 
                Doc = d, 
                Score = keywords.Sum(kw => 
                    (d.Title.ToLower().Contains(kw) ? 3 : 0) + 
                    (d.Content.ToLower().Split(kw).Length - 1))
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
        
        if (bestDoc != null && bestDoc.Score > 0)
        {
            // Use intelligent answer generation
            var answer = GenerateDirectAnswer(userMessage, bestDoc.Doc.Content);
            return answer;
        }
        
        return GetNoDocumentsResponse(userMessage);
    }
    
    private string GetNoDocumentsResponse(string userMessage)
    {
        var lower = userMessage.ToLower();
        
        if (lower.Contains("hello") || lower.Contains("hi"))
        {
            return "Hello! Add some notes or PDFs in the Documents tab, then I can answer questions about them.";
        }
        
        if (lower.Contains("what can you do"))
        {
            return "I can answer questions about your documents, summarize content, and find specific information. Add some notes or PDFs to get started!";
        }
        
        if (lower.Contains("document") || lower.Contains("note") || lower.Contains("pdf"))
        {
            return "Please add documents first! Go to the Documents tab to add notes or import PDFs.";
        }
        
        return "Add some notes or import PDFs in the Documents tab, then ask me questions about them!";
    }
    
    public async Task<List<ChatMessage>> GetChatHistoryAsync()
    {
        return await _databaseService.GetChatMessagesAsync();
    }
    
    public async Task SaveMessageAsync(ChatMessage message)
    {
        await _databaseService.InsertChatMessageAsync(message);
    }
    
    public async Task ClearHistoryAsync()
    {
        await _databaseService.ClearChatMessagesAsync();
    }
    
    public void Dispose()
    {
        _kernel = null;
    }
}
