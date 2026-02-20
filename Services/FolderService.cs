using MauMind.App.Data;
using MauMind.App.Models;

namespace MauMind.App.Services;

public class FolderService : IFolderService
{
    private readonly DatabaseService _databaseService;

    public FolderService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    // ─── Folder CRUD ─────────────────────────────────────────────────────────────

    public async Task<int> CreateFolderAsync(string name, string? color = null, string? icon = null, int? parentFolderId = null)
    {
        var folder = new Folder
        {
            Name = name,
            Color = color,
            Icon = icon ?? "📂",
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _databaseService.InsertFolderAsync(folder);
    }

    public async Task<Folder?> GetFolderAsync(int id)
    {
        return await _databaseService.GetFolderByIdAsync(id);
    }

    public async Task<List<Folder>> GetAllFoldersAsync()
    {
        return await _databaseService.GetAllFoldersAsync();
    }

    public async Task<List<Folder>> GetRootFoldersAsync()
    {
        var allFolders = await _databaseService.GetAllFoldersAsync();
        return allFolders.Where(f => f.ParentFolderId == null).ToList();
    }

    public async Task<List<Folder>> GetSubFoldersAsync(int parentFolderId)
    {
        var allFolders = await _databaseService.GetAllFoldersAsync();
        return allFolders.Where(f => f.ParentFolderId == parentFolderId).ToList();
    }

    public async Task UpdateFolderAsync(Folder folder)
    {
        folder.UpdatedAt = DateTime.UtcNow;
        await _databaseService.UpdateFolderAsync(folder);
    }

    public async Task DeleteFolderAsync(int id)
    {
        // Move documents to uncategorized
        var documents = await GetDocumentsInFolderAsync(id);
        foreach (var doc in documents)
        {
            await MoveDocumentToFolderAsync(doc.Id, null);
        }

        // Move sub-folders to root
        var subFolders = await GetSubFoldersAsync(id);
        foreach (var subFolder in subFolders)
        {
            subFolder.ParentFolderId = null;
            await UpdateFolderAsync(subFolder);
        }

        // Delete folder
        await _databaseService.DeleteFolderAsync(id);
    }

    // ─── Document-Folder Operations ───────────────────────────────────────────────

    public async Task MoveDocumentToFolderAsync(int documentId, int? folderId)
    {
        await _databaseService.UpdateDocumentFolderAsync(documentId, folderId);
    }

    public async Task<List<Document>> GetDocumentsInFolderAsync(int folderId)
    {
        return await _databaseService.GetDocumentsByFolderAsync(folderId);
    }

    public async Task<List<Document>> GetUncategorizedDocumentsAsync()
    {
        return await _databaseService.GetDocumentsByFolderAsync(null);
    }

    // ─── Utility ─────────────────────────────────────────────────────────────────

    public async Task<Folder?> GetFolderWithDocumentsAsync(int id)
    {
        var folder = await GetFolderAsync(id);
        if (folder == null) return null;

        folder.Documents = await GetDocumentsInFolderAsync(id);
        folder.SubFolders = await GetSubFoldersAsync(id);

        return folder;
    }

    public async Task<Dictionary<Folder, int>> GetFolderDocumentCountsAsync()
    {
        var folders = await GetAllFoldersAsync();
        var counts = new Dictionary<Folder, int>();

        foreach (var folder in folders)
        {
            var docs = await GetDocumentsInFolderAsync(folder.Id);
            counts[folder] = docs.Count;
        }

        return counts;
    }
}