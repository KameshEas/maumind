using MauMind.App.Data;
using MauMind.App.Models;
using UglyToad.PdfPig;

namespace MauMind.App.Services;

public class DocumentService : IDocumentService
{
    private readonly DatabaseService _databaseService;
    private readonly IVectorStore _vectorStore;
    private readonly IChatService _chatService;

    public DocumentService(DatabaseService databaseService, IVectorStore vectorStore, IChatService chatService)
    {
        _databaseService = databaseService;
        _vectorStore = vectorStore;
        _chatService = chatService;
    }
    
    public async Task<int> AddNoteAsync(string title, string content)
    {
        var document = new Document
        {
            Title = title,
            Content = content,
            SourceType = DocumentSourceType.Note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var id = await _databaseService.InsertDocumentAsync(document);
        
        // Add to vector store
        await _vectorStore.AddDocumentVectorsAsync(id, content);
        
        return id;
    }
    
    public async Task<int> AddPdfAsync(string title, string filePath)
    {
        var content = await ExtractPdfTextAsync(filePath);
        
        var document = new Document
        {
            Title = title,
            Content = content,
            SourceType = DocumentSourceType.PDF,
            FilePath = filePath,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var id = await _databaseService.InsertDocumentAsync(document);
        
        // Add to vector store
        await _vectorStore.AddDocumentVectorsAsync(id, content);
        
        return id;
    }
    
    public async Task<int> AddLogAsync(string title, string content)
    {
        var document = new Document
        {
            Title = title,
            Content = content,
            SourceType = DocumentSourceType.Log,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var id = await _databaseService.InsertDocumentAsync(document);
        
        // Add to vector store
        await _vectorStore.AddDocumentVectorsAsync(id, content);
        
        return id;
    }
    
    public async Task<List<Document>> GetAllDocumentsAsync()
    {
        return await _databaseService.GetAllDocumentsAsync();
    }
    
    public async Task<Document?> GetDocumentAsync(int id)
    {
        return await _databaseService.GetDocumentByIdAsync(id);
    }
    
    public async Task DeleteDocumentAsync(int id)
    {
        // Delete from vector store first
        await _vectorStore.DeleteDocumentVectorsAsync(id);
        
        // Delete from database
        await _databaseService.DeleteDocumentAsync(id);
    }
    
    private async Task<string> ExtractPdfTextAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var document = PdfDocument.Open(filePath);
                var textBuilder = new System.Text.StringBuilder();

                foreach (var page in document.GetPages())
                {
                    textBuilder.AppendLine(page.Text);
                }

                return textBuilder.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting PDF: {ex.Message}");
                return string.Empty;
            }
        });
    }

    // ─── Smart Summarization ───────────────────────────────────────────────────

    public async Task<string> SummarizeDocumentAsync(int documentId)
    {
        var document = await GetDocumentAsync(documentId);
        if (document == null)
            return "Document not found.";

        // Return cached summary if exists
        if (!string.IsNullOrEmpty(document.Summary))
            return document.Summary;

        // Generate summary using AI
        var contentToSummarize = document.Content.Length > 3000
            ? document.Content[..3000] + "..."
            : document.Content;

        var prompt = $@"Summarize the following document in 3-5 bullet points. Focus on key facts, dates, names, and actionable information:

Title: {document.Title}

Content:
{contentToSummarize}

Provide only the bullet points (use • for bullets), no preamble or introduction.";

        var summaryBuilder = new System.Text.StringBuilder();
        await foreach (var token in _chatService.GetStreamingResponseAsync(prompt))
        {
            summaryBuilder.Append(token);
        }

        var summary = summaryBuilder.ToString().Trim();

        // Cache the summary
        await UpdateDocumentSummaryAsync(documentId, summary);

        return summary;
    }

    public async Task UpdateDocumentSummaryAsync(int documentId, string summary)
    {
        await _databaseService.UpdateDocumentSummaryAsync(documentId, summary, DateTime.UtcNow);
    }
}
