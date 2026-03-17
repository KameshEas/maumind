using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IMemoryService
{
    Task<int> AddMemoryAsync(string title, string content, bool pinned = false);
    Task<List<(MemoryEntry Memory, float Score)>> RetrieveAsync(string query, int topK = 5);
    Task<List<MemoryEntry>> GetAllMemoriesAsync();
    Task<MemoryEntry?> GetMemoryByIdAsync(int id);
    Task DeleteMemoryAsync(int id);
}
