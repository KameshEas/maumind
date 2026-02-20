using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauMind.App.Models;
using MauMind.App.Services;
using System.Collections.ObjectModel;

namespace MauMind.App.ViewModels;

public partial class DocumentsViewModel : ObservableObject
{
    private readonly IDocumentService _documentService;
    private readonly IFolderService _folderService;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

    [ObservableProperty]
    private ObservableCollection<Folder> _folders = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _isFolderView;

    [ObservableProperty]
    private Folder? _currentFolder;

    [ObservableProperty]
    private string _currentFolderPath = "All Documents";

    public int DocumentCount => Documents.Count;
    public int FolderCount => Folders.Count;
    public int UncategorizedCount { get; private set; }

    // Add Note fields
    [ObservableProperty]
    private string _noteTitle = string.Empty;

    [ObservableProperty]
    private string _noteContent = string.Empty;

    // Add Log fields
    [ObservableProperty]
    private string _logTitle = string.Empty;

    [ObservableProperty]
    private string _logContent = string.Empty;

    // Selected filter
    [ObservableProperty]
    private string _selectedFilter = "All";

    // New folder dialog
    [ObservableProperty]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    private string _newFolderColor = "#0078D4";

    public DocumentsViewModel(IDocumentService documentService, IFolderService folderService)
    {
        _documentService = documentService;
        _folderService = folderService;
    }

    public async Task InitializeAsync()
    {
        await LoadFoldersAsync();
        await LoadDocumentsAsync();
    }

    [RelayCommand]
    private async Task LoadDocuments()
    {
        await LoadDocumentsAsync();
    }

    private async Task LoadDocumentsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading documents...";

        try
        {
            List<Document> docs;

            if (CurrentFolder != null)
            {
                // Load documents in current folder
                docs = await _folderService.GetDocumentsInFolderAsync(CurrentFolder.Id);
            }
            else if (IsFolderView)
            {
                // Show uncategorized documents
                docs = await _folderService.GetUncategorizedDocumentsAsync();
            }
            else
            {
                // Show all documents
                docs = await _documentService.GetAllDocumentsAsync();
            }

            // Apply filter
            if (SelectedFilter != "All")
            {
                var filterType = Enum.Parse<DocumentSourceType>(SelectedFilter);
                docs = docs.Where(d => d.SourceType == filterType).ToList();
            }

            Documents.Clear();
            foreach (var doc in docs)
            {
                Documents.Add(doc);
            }

            IsEmpty = Documents.Count == 0;
            StatusMessage = $"{Documents.Count} documents";
            OnPropertyChanged(nameof(DocumentCount));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadFoldersAsync()
    {
        try
        {
            var folders = await _folderService.GetRootFoldersAsync();
            var uncategorized = await _folderService.GetUncategorizedDocumentsAsync();
            UncategorizedCount = uncategorized.Count;

            Folders.Clear();
            foreach (var folder in folders)
            {
                Folders.Add(folder);
            }

            OnPropertyChanged(nameof(FolderCount));
            OnPropertyChanged(nameof(UncategorizedCount));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading folders: {ex.Message}";
        }
    }

    // ─── Folder Navigation ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenFolder(Folder? folder)
    {
        if (folder == null) return;

        CurrentFolder = folder;
        CurrentFolderPath = $"📂 {folder.Name}";
        IsFolderView = false;
        await LoadDocumentsAsync();
    }

    [RelayCommand]
    private async Task ShowUncategorized()
    {
        CurrentFolder = null;
        CurrentFolderPath = "📄 Uncategorized";
        IsFolderView = true;
        await LoadDocumentsAsync();
    }

    [RelayCommand]
    private async Task ShowAllDocuments()
    {
        CurrentFolder = null;
        CurrentFolderPath = "All Documents";
        IsFolderView = false;
        await LoadDocumentsAsync();
    }

    // ─── Folder Management ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateFolder()
    {
        if (string.IsNullOrWhiteSpace(NewFolderName))
        {
            StatusMessage = "Please enter a folder name";
            return;
        }

        IsLoading = true;
        StatusMessage = "Creating folder...";

        try
        {
            await _folderService.CreateFolderAsync(NewFolderName, NewFolderColor, "📂");
            NewFolderName = string.Empty;

            await LoadFoldersAsync();
            StatusMessage = "Folder created!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteFolder(Folder? folder)
    {
        if (folder == null) return;

        IsLoading = true;

        try
        {
            await _folderService.DeleteFolderAsync(folder.Id);
            Folders.Remove(folder);

            // If we were viewing this folder, go back to all documents
            if (CurrentFolder?.Id == folder.Id)
            {
                await ShowAllDocuments();
            }

            await LoadFoldersAsync();
            StatusMessage = "Folder deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MoveDocumentToFolder(Document? document)
    {
        if (document == null) return;
        // This will be handled by a folder picker popup in the view
    }

    public async Task MoveDocumentToFolderAsync(int documentId, int? folderId)
    {
        try
        {
            await _folderService.MoveDocumentToFolderAsync(documentId, folderId);
            await LoadDocumentsAsync();
            await LoadFoldersAsync();
            StatusMessage = folderId.HasValue ? "Document moved to folder" : "Document moved to Uncategorized";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    // ─── Document Operations ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddNote()
    {
        if (string.IsNullOrWhiteSpace(NoteTitle) || string.IsNullOrWhiteSpace(NoteContent))
        {
            StatusMessage = "Please enter title and content";
            return;
        }

        IsLoading = true;
        StatusMessage = "Adding note...";

        try
        {
            await _documentService.AddNoteAsync(NoteTitle, NoteContent);

            NoteTitle = string.Empty;
            NoteContent = string.Empty;

            await LoadDocumentsAsync();
            await LoadFoldersAsync();
            StatusMessage = "Note added successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddLog()
    {
        if (string.IsNullOrWhiteSpace(LogTitle) || string.IsNullOrWhiteSpace(LogContent))
        {
            StatusMessage = "Please enter title and content";
            return;
        }

        IsLoading = true;
        StatusMessage = "Adding log entry...";

        try
        {
            await _documentService.AddLogAsync(LogTitle, LogContent);

            LogTitle = string.Empty;
            LogContent = string.Empty;

            await LoadDocumentsAsync();
            await LoadFoldersAsync();
            StatusMessage = "Log entry added successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddPdf(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusMessage = "Please select a PDF file";
            return;
        }

        IsLoading = true;
        StatusMessage = "Processing PDF...";

        try
        {
            var title = Path.GetFileNameWithoutExtension(filePath);
            await _documentService.AddPdfAsync(title, filePath);

            await LoadDocumentsAsync();
            await LoadFoldersAsync();
            StatusMessage = "PDF imported successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteDocument(Document? document)
    {
        if (document == null) return;

        IsLoading = true;

        try
        {
            await _documentService.DeleteDocumentAsync(document.Id);
            Documents.Remove(document);
            await LoadFoldersAsync();
            StatusMessage = "Document deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SetFilter(string filter)
    {
        SelectedFilter = filter;
        await LoadDocumentsAsync();
    }
}