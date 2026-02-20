using System.Text.Json;
using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IDataService
{
    Task<string> ExportDataAsync();
    Task<bool> ImportDataAsync(string jsonData);
    Task<string> GetExportFileName();
}

public class DataService : IDataService
{
    private readonly IDocumentService _documentService;
    private readonly IChatService _chatService;
    private readonly IVectorStore _vectorStore;
    
    public DataService(IDocumentService documentService, IChatService chatService, IVectorStore vectorStore)
    {
        _documentService = documentService;
        _chatService = chatService;
        _vectorStore = vectorStore;
    }
    
    public async Task<string> GetExportFileName()
    {
        return $"maumind_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
    }
    
    public async Task<string> ExportDataAsync()
    {
        var exportData = new ExportData
        {
            ExportDate = DateTime.UtcNow,
            Version = "1.0.0",
            Documents = await _documentService.GetAllDocumentsAsync(),
            ChatMessages = await _chatService.GetChatHistoryAsync()
        };
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        return JsonSerializer.Serialize(exportData, options);
    }
    
    public async Task<bool> ImportDataAsync(string jsonData)
    {
        try
        {
            var importData = JsonSerializer.Deserialize<ExportData>(jsonData);
            
            if (importData == null) return false;
            
            // Import documents
            if (importData.Documents != null)
            {
                foreach (var doc in importData.Documents)
                {
                    // Use appropriate method based on source type
                    if (doc.SourceType == DocumentSourceType.PDF)
                    {
                        // Skip PDFs as they need file paths
                    }
                    else if (doc.SourceType == DocumentSourceType.Log)
                    {
                        await _documentService.AddLogAsync(doc.Title, doc.Content);
                    }
                    else
                    {
                        await _documentService.AddNoteAsync(doc.Title, doc.Content);
                    }
                }
            }
            
            // Import chat messages
            if (importData.ChatMessages != null)
            {
                foreach (var msg in importData.ChatMessages)
                {
                    await _chatService.SaveMessageAsync(msg);
                }
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class ExportData
{
    public DateTime ExportDate { get; set; }
    public string Version { get; set; } = "1.0.0";
    public List<Document>? Documents { get; set; }
    public List<ChatMessage>? ChatMessages { get; set; }
}
