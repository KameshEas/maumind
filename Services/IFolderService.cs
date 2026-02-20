using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IFolderService
{
    // Folder CRUD
    Task<int> CreateFolderAsync(string name, string? color = null, string? icon = null, int? parentFolderId = null);
    Task<Folder?> GetFolderAsync(int id);
    Task<List<Folder>> GetAllFoldersAsync();
    Task<List<Folder>> GetRootFoldersAsync();
    Task<List<Folder>> GetSubFoldersAsync(int parentFolderId);
    Task UpdateFolderAsync(Folder folder);
    Task DeleteFolderAsync(int id);

    // Document-Folder Operations
    Task MoveDocumentToFolderAsync(int documentId, int? folderId);
    Task<List<Document>> GetDocumentsInFolderAsync(int folderId);
    Task<List<Document>> GetUncategorizedDocumentsAsync();

    // Utility
    Task<Folder?> GetFolderWithDocumentsAsync(int id);
    Task<Dictionary<Folder, int>> GetFolderDocumentCountsAsync();
}