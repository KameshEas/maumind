using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauMind.App.Models;
using MauMind.App.Services;
using System.Collections.ObjectModel;

namespace MauMind.App.ViewModels;

public partial class NoteEditorViewModel : ObservableObject
{
    private readonly IWritingAssistantService _writingAssistant;
    private readonly IDocumentService _documentService;
    private readonly IFolderService _folderService;
    private int? _documentId;
    private int? _folderId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _hasSuggestions;

    [ObservableProperty]
    private ObservableCollection<WritingSuggestion> _suggestions = new();

    [ObservableProperty]
    private List<ToneType> _toneOptions = new() 
    { 
        ToneType.Professional, 
        ToneType.Casual, 
        ToneType.Formal, 
        ToneType.Academic, 
        ToneType.Friendly 
    };

    [ObservableProperty]
    private ToneType _selectedTone = ToneType.Professional;

    public event EventHandler? SaveCompleted;
    public event EventHandler? CancelRequested;

    public NoteEditorViewModel(
        IWritingAssistantService writingAssistant,
        IDocumentService documentService,
        IFolderService folderService)
    {
        _writingAssistant = writingAssistant;
        _documentService = documentService;
        _folderService = folderService;
    }

    public void Initialize(int? documentId = null, int? folderId = null, string? initialTitle = null, string? initialContent = null)
    {
        _documentId = documentId;
        _folderId = folderId;

        if (documentId.HasValue)
        {
            // Load existing document
            _ = LoadDocumentAsync(documentId.Value);
        }
        else
        {
            Title = initialTitle ?? string.Empty;
            Content = initialContent ?? string.Empty;
            StatusMessage = "New note";
        }
    }

    private async Task LoadDocumentAsync(int docId)
    {
        var doc = await _documentService.GetDocumentAsync(docId);
        if (doc != null)
        {
            Title = doc.Title;
            Content = doc.Content;
            _folderId = doc.FolderId;
            StatusMessage = "Loaded";
        }
    }

    [RelayCommand]
    private async Task Analyze()
    {
        if (string.IsNullOrWhiteSpace(Content) || Content.Length < 10)
        {
            StatusMessage = "Write more to get suggestions...";
            return;
        }

        IsAnalyzing = true;
        StatusMessage = "Analyzing...";

        try
        {
            var suggestions = await _writingAssistant.AnalyzeTextAsync(Content);
            Suggestions.Clear();

            foreach (var suggestion in suggestions)
            {
                Suggestions.Add(suggestion);
            }

            HasSuggestions = Suggestions.Count > 0;
            StatusMessage = HasSuggestions ? $"Found {Suggestions.Count} suggestions" : "No issues found!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private async Task Rewrite()
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            StatusMessage = "Write something first...";
            return;
        }

        IsAnalyzing = true;
        StatusMessage = "Rewriting...";

        try
        {
            var rewritten = await _writingAssistant.RewriteForToneAsync(Content, SelectedTone);
            Content = rewritten;
            StatusMessage = "Rewritten! Review changes.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rewrite failed: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    public async Task AcceptSuggestionAsync(WritingSuggestion suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion.SuggestedText))
            return;

        // Replace the text
        var newContent = Content.Remove(suggestion.StartIndex, suggestion.OriginalText.Length)
                                .Insert(suggestion.StartIndex, suggestion.SuggestedText);

        Content = newContent;
        suggestion.IsAccepted = true;
        Suggestions.Remove(suggestion);
        HasSuggestions = Suggestions.Count > 0;
        StatusMessage = "Suggestion applied";

        // Re-analyze to update suggestion positions
        await Analyze();
    }

    public void IgnoreSuggestion(WritingSuggestion suggestion)
    {
        suggestion.IsIgnored = true;
        Suggestions.Remove(suggestion);
        HasSuggestions = Suggestions.Count > 0;
        StatusMessage = "Suggestion ignored";
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Please enter a title";
            return;
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            StatusMessage = "Please enter some content";
            return;
        }

        StatusMessage = "Saving...";

        try
        {
            if (_documentId.HasValue)
            {
                // Update existing document
                await _documentService.AddNoteAsync(Title, Content); // Note: This creates new, need update method
                StatusMessage = "Note saved!";
            }
            else
            {
                // Create new document
                await _documentService.AddNoteAsync(Title, Content);
                StatusMessage = "Note created!";
            }

            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}