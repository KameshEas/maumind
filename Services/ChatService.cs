using MauMind.App.Data;
using MauMind.App.Models;
using Microsoft.SemanticKernel;
using System.Runtime.CompilerServices;

namespace MauMind.App.Services;

public class ChatService : IChatService, IAsyncDisposable, IDisposable
{
    private readonly IVectorStore _vectorStore;
    private readonly DatabaseService _databaseService;
    private Kernel? _kernel;
    private bool _isModelLoaded;
    private string _modelPath = string.Empty;

    public bool IsModelLoaded => _isModelLoaded;

    public event EventHandler<string>? NoLocalDataFound;
    public event EventHandler<List<ProvenanceEntry>>? ProvenanceAvailable;

    public ChatService(IVectorStore vectorStore, DatabaseService databaseService)
    {
        _vectorStore = vectorStore;
        _databaseService = databaseService;
    }

    public async Task LoadModelAsync(IProgress<int>? progress = null)
    {
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

        List<(VectorEntry Entry, float Score)> contextResults;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            contextResults = await _vectorStore.SearchAsync(userMessage, topK: 5).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Vector search timed out after 30 seconds");
            contextResults = new List<(VectorEntry, float)>();
        }
        catch
        {
            contextResults = new List<(VectorEntry, float)>();
        }

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

        string response;
        bool noLocalData = false;

        if (contextResults.Count > 0 && contextResults[0].Score > 0.25f)
        {
            response = GenerateResponseFromContext(userMessage, contextResults);
        }
        else if (allDocuments.Count > 0)
        {
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
            noLocalData = true;
            response = string.Empty;
        }

        if (noLocalData)
        {
            NoLocalDataFound?.Invoke(this, userMessage);
            yield return "__NEEDS_WEB_SEARCH__";
            yield break;
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            response = "I found some relevant information in your documents. Could you try rephrasing your question?";
        }

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
        var relevantChunk = results.FirstOrDefault(r => r.Score > 0.3);

        if (relevantChunk.Entry == null)
            return GetNoDocumentsResponse(userMessage);

        var chunkText = relevantChunk.Entry.ChunkText;
        var answer = GenerateDirectAnswer(userMessage, chunkText);

        return answer;
    }

    private string GenerateDirectAnswer(string question, string context)
    {
        var contextLower = context.ToLower();
        var questionLower = question.ToLower();

        bool isWhatQuestion = questionLower.Contains("what");
        bool isListQuestion = questionLower.Contains("list") || questionLower.Contains("name");
        bool isHowQuestion = questionLower.Contains("how");

        var sentences = context.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var relevantSentences = new List<string>();

        var questionWords = questionLower
            .Split(new[] { ' ', ',', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();

        foreach (var sentence in sentences)
        {
            var sentenceLower = sentence.ToLower();
            int matchCount = questionWords.Count(qw => sentenceLower.Contains(qw));
            if (matchCount > 0)
            {
                relevantSentences.Add(sentence.Trim());
            }
        }

        if (relevantSentences.Count > 0)
        {
            var answer = string.Join(". ", relevantSentences.Take(2));
            if (!answer.EndsWith(".")) answer += ".";

            if (isWhatQuestion)
                return $"What I found: {answer}";
            if (isListQuestion)
                return $"Here are the items: {answer}";
            if (isHowQuestion)
                return $"Here's how: {answer}";
            return $"Answer: {answer}";
        }

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

        var bestDoc = documents
            .Select(d => new
            {
                Doc = d,
                Score = keywords.Sum(kw =>
                    (d.Title.ToLower().Contains(kw) ? 3 : 0) +
                    (d.Content.ToLower().Split(kw).Length - 1))
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestDoc != null && bestDoc.Score > 0)
        {
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
        try
        {
            // Kernel disposal is not attempted here because Kernel may not implement IDisposable
        }
        catch { }
        _kernel = null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Do not attempt to dispose Kernel here to avoid type compatibility issues
            _kernel = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatService] DisposeAsync error: {ex.Message}");
        }
    }
}
