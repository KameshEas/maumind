namespace MauMind.App.Models;

public class Folder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }       // Hex color: #FF5722, #2196F3, etc.
    public string? Icon { get; set; }        // Emoji icon: 📂, 🗂️, etc.
    public int? ParentFolderId { get; set; } // For nested folders (null = root)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Folder? ParentFolder { get; set; }
    public List<Folder> SubFolders { get; set; } = new();
    public List<Document> Documents { get; set; } = new();

    // Computed
    public int DocumentCount => Documents.Count;
    public int TotalDocumentCount => Documents.Count + SubFolders.Sum(f => f.TotalDocumentCount);
}