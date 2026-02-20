using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IDocumentService
{
    Task<int> AddNoteAsync(string title, string content);
    Task<int> AddPdfAsync(string title, string filePath);
    Task<int> AddLogAsync(string title, string content);
    Task<List<Document>> GetAllDocumentsAsync();
    Task<Document?> GetDocumentAsync(int id);
    Task DeleteDocumentAsync(int id);

    /// <summary>
    /// Generate and cache a summary for the document using AI.
    /// </summary>
    Task<string> SummarizeDocumentAsync(int documentId);

    /// <summary>
    /// Update document with cached summary.
    /// </summary>
    Task UpdateDocumentSummaryAsync(int documentId, string summary);
}
