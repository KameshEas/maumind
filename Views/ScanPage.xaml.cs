using MauMind.App.Services;

namespace MauMind.App.Views;

public partial class ScanPage : ContentPage
{
    private readonly IDocumentScanService _scanService;
    private readonly IDocumentService _documentService;
    private string? _currentImagePath;

    public ScanPage()
    {
        InitializeComponent();
        
        _scanService = App.GetService<IDocumentScanService>();
        _documentService = App.GetService<IDocumentService>();
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        Navigation.PopAsync();
    }

    private async void OnCameraScanTapped(object? sender, EventArgs e)
    {
        await ScanWithCameraAsync();
    }

    private async void OnGalleryScanTapped(object? sender, EventArgs e)
    {
        await ScanWithGalleryAsync();
    }

    private async Task ScanWithCameraAsync()
    {
        // Show loading
        LoadingStack.IsVisible = true;
        ResultsStack.IsVisible = false;

        try
        {
            var result = await _scanService.ScanDocumentAsync();
            
            if (result.Success)
            {
                _currentImagePath = result.ImagePath;
                ExtractedTextEditor.Text = result.ExtractedText;
                ResultsStack.IsVisible = true;
            }
            else
            {
                await DisplayAlert("Info", result.ErrorMessage ?? "Camera scanning is not yet available", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to scan: {ex.Message}", "OK");
        }
        finally
        {
            LoadingStack.IsVisible = false;
        }
    }

    private async Task ScanWithGalleryAsync()
    {
        // Show loading
        LoadingStack.IsVisible = true;
        ResultsStack.IsVisible = false;

        try
        {
            var result = await _scanService.ScanFromGalleryAsync();
            
            if (result.Success)
            {
                _currentImagePath = result.ImagePath;
                ExtractedTextEditor.Text = result.ExtractedText;
                ResultsStack.IsVisible = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(result.ErrorMessage) && result.ErrorMessage != "No image selected")
                {
                    await DisplayAlert("Error", result.ErrorMessage, "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to scan: {ex.Message}", "OK");
        }
        finally
        {
            LoadingStack.IsVisible = false;
        }
    }

    private async void OnRescanClicked(object? sender, EventArgs e)
    {
        ResultsStack.IsVisible = false;
        ExtractedTextEditor.Text = string.Empty;
        _currentImagePath = null;
        
        // Let user choose again
        var choice = await DisplayActionSheet("Choose Scan Method", "Cancel", null, "Camera", "Gallery");
        
        if (choice == "Camera")
        {
            await ScanWithCameraAsync();
        }
        else if (choice == "Gallery")
        {
            await ScanWithGalleryAsync();
        }
    }

    private async void OnSaveDocumentClicked(object? sender, EventArgs e)
    {
        var extractedText = ExtractedTextEditor.Text;
        
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            await DisplayAlert("Error", "No text to save. Please scan a document first.", "OK");
            return;
        }

        // Ask for document title
        var title = await DisplayPromptAsync(
            "Save Document",
            "Enter a title for this document:",
            "Save",
            "Cancel",
            "Scanned Document",
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        try
        {
            // Save as scanned document
            var docId = await _documentService.AddNoteAsync(title, extractedText);
            
            await DisplayAlert("Success", "Document saved successfully!", "OK");
            
            // Navigate back to documents page
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save document: {ex.Message}", "OK");
        }
    }
}
