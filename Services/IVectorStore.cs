using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IVectorStore
{
    Task AddDocumentVectorsAsync(int documentId, string content);
    Task<List<(VectorEntry Entry, float Score)>> SearchAsync(string query, int topK = 5);
    Task DeleteDocumentVectorsAsync(int documentId);
    Task InitializeAsync();
}
