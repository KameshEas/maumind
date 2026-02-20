using MauMind.App.Models;
using MauMind.App.ViewModels;

namespace MauMind.App.Views;

public partial class NoteEditorPage : ContentPage
{
    private readonly NoteEditorViewModel _viewModel;

    public NoteEditorPage(int? documentId = null, int? folderId = null, string? initialTitle = null, string? initialContent = null)
    {
        InitializeComponent();
        _viewModel = App.GetService<NoteEditorViewModel>();
        BindingContext = _viewModel;

        // Initialize with document or folder
        _viewModel.Initialize(documentId, folderId, initialTitle, initialContent);

        // Handle events
        _viewModel.SaveCompleted += OnSaveCompleted;
        _viewModel.CancelRequested += OnCancelRequested;
    }

    private async void OnSaveCompleted(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnCancelRequested(object? sender, EventArgs e)
    {
        var confirm = true;
        if (!string.IsNullOrWhiteSpace(_viewModel.Title) || !string.IsNullOrWhiteSpace(_viewModel.Content))
        {
            confirm = await DisplayAlert("Discard Changes?", "You have unsaved changes. Are you sure you want to discard them?", "Discard", "Keep Editing");
        }

        if (confirm)
        {
            await Navigation.PopModalAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.SaveCompleted -= OnSaveCompleted;
        _viewModel.CancelRequested -= OnCancelRequested;
    }

    // ─── Suggestion Actions ───────────────────────────────────────────────────────

    private async void OnAcceptSuggestion(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is WritingSuggestion suggestion)
        {
            await _viewModel.AcceptSuggestionAsync(suggestion);
        }
    }

    private void OnIgnoreSuggestion(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is WritingSuggestion suggestion)
        {
            _viewModel.IgnoreSuggestion(suggestion);
        }
    }
}
