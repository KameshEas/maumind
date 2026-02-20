using MauMind.App.Models;
using MauMind.App.Services;
using MauMind.App.ViewModels;
using Microsoft.Maui.Controls;

namespace MauMind.App.Views;

public partial class DocumentsPage : ContentPage
{
    private readonly DocumentsViewModel _viewModel;
    private readonly IAnimationService _animationService;
    private readonly IDocumentService _documentService;
    private readonly IFolderService _folderService;

    public DocumentsPage()
    {
        InitializeComponent();

        _viewModel = App.GetService<DocumentsViewModel>();
        _animationService = App.GetService<IAnimationService>();
        _documentService = App.GetService<IDocumentService>();
        _folderService = App.GetService<IFolderService>();
        BindingContext = _viewModel;

        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync();
            RefreshFolders();
            RefreshDocuments();
            _viewModel.Folders.CollectionChanged += (sender, args) => RefreshFolders();
            _viewModel.Documents.CollectionChanged += (sender, args) => RefreshDocuments();
        };
    }

    private void RefreshFolders()
    {
        FoldersStack.Children.Clear();

        foreach (var folder in _viewModel.Folders)
        {
            var card = CreateFolderCard(folder);
            FoldersStack.Children.Add(card);
        }
    }

    private Frame CreateFolderCard(Folder folder)
    {
        var color = !string.IsNullOrEmpty(folder.Color) ? Color.FromArgb(folder.Color) : Color.FromArgb("#0078D4");

        var card = new Frame
        {
            CornerRadius = 12,
            Padding = new Thickness(12, 10),
            BackgroundColor = color.WithAlpha(0.15f),
            HasShadow = false,
            Margin = new Thickness(0, 4)
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await _viewModel.OpenFolderCommand.ExecuteAsync(folder);
        card.GestureRecognizers.Add(tapGesture);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        // Icon
        var iconLabel = new Label
        {
            Text = folder.Icon ?? "📂",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(iconLabel, 0);

        // Name
        var nameLabel = new Label
        {
            Text = folder.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(nameLabel, 1);

        // Menu button
        var menuBtn = new Button
        {
            Text = "⋮",
            FontSize = 16,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.Gray,
            WidthRequest = 32,
            HeightRequest = 32,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };

        menuBtn.Clicked += async (s, e) =>
        {
            var action = await DisplayActionSheet(folder.Name, "Cancel", "Delete", "Rename", "Move Documents");
            if (action == "Delete")
            {
                var confirm = await DisplayAlert("Delete Folder", $"Delete '{folder.Name}'? Documents will be moved to Uncategorized.", "Delete", "Cancel");
                if (confirm)
                {
                    await _viewModel.DeleteFolderCommand.ExecuteAsync(folder);
                }
            }
            else if (action == "Rename")
            {
                var newName = await DisplayPromptAsync("Rename Folder", "Enter new name:", "Rename", "Cancel", folder.Name);
                if (!string.IsNullOrWhiteSpace(newName) && newName != folder.Name)
                {
                    folder.Name = newName;
                    await _folderService.UpdateFolderAsync(folder);
                    RefreshFolders();
                }
            }
        };

        Grid.SetColumn(menuBtn, 2);

        grid.Children.Add(iconLabel);
        grid.Children.Add(nameLabel);
        grid.Children.Add(menuBtn);

        card.Content = grid;
        return card;
    }

    private void RefreshDocuments()
    {
        DocumentsStack.Children.Clear();

        // Show/hide empty state
        DocumentsEmptyState.IsVisible = _viewModel.Documents.Count == 0;

        foreach (var doc in _viewModel.Documents)
        {
            var card = CreateDocumentCard(doc);
            DocumentsStack.Children.Add(card);
        }
    }

    private Frame CreateDocumentCard(Document doc)
    {
        var typeIcon = doc.SourceType switch
        {
            DocumentSourceType.Note => "📝",
            DocumentSourceType.Log => "📔",
            DocumentSourceType.PDF => "📄",
            _ => "📄"
        };

        var typeColor = doc.SourceType switch
        {
            DocumentSourceType.Note => Color.FromArgb("#E3F2FD"),
            DocumentSourceType.Log => Color.FromArgb("#FFF3E0"),
            DocumentSourceType.PDF => Color.FromArgb("#FCE4EC"),
            _ => Colors.White
        };

        var card = new Frame
        {
            CornerRadius = 16,
            Padding = new Thickness(16, 12),
            BackgroundColor = typeColor,
            HasShadow = true,
            Opacity = 0,
            TranslationY = 16
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        // Icon
        var iconLabel = new Label
        {
            Text = typeIcon,
            FontSize = 28,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(iconLabel, 0);

        // Content
        var contentStack = new StackLayout
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalOptions = LayoutOptions.Center
        };

        contentStack.Children.Add(new Label
        {
            Text = doc.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
            LineBreakMode = LineBreakMode.TailTruncation
        });

        contentStack.Children.Add(new Label
        {
            Text = doc.Content.Length > 80 ? doc.Content[..80] + "..." : doc.Content,
            FontSize = 12,
            TextColor = Color.FromArgb("#666666"),
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        contentStack.Children.Add(new Label
        {
            Text = doc.CreatedAt.ToString("MMM dd, yyyy"),
            FontSize = 10,
            TextColor = Color.FromArgb("#999999"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        Grid.SetColumn(contentStack, 1);

        // Summary indicator (if exists)
        var hasSummary = !string.IsNullOrEmpty(doc.Summary);

        // Make the card tappable for editing
        var cardTapGesture = new TapGestureRecognizer();
        cardTapGesture.Tapped += async (s, e) =>
        {
            if (doc.SourceType == DocumentSourceType.Note)
            {
                await EditDocument(doc);
            }
        };
        card.GestureRecognizers.Add(cardTapGesture);

        // Button stack
        var btnStack = new HorizontalStackLayout { Spacing = 4 };

        // Edit button (for notes)
        if (doc.SourceType == DocumentSourceType.Note)
        {
            var editBtn = new Button
            {
                Text = "✏️",
                FontSize = 14,
                BackgroundColor = Colors.Transparent,
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                VerticalOptions = LayoutOptions.Center
            };
            SemanticProperties.SetHint(editBtn, "Edit note");

            editBtn.Clicked += async (s, e) =>
            {
                await EditDocument(doc);
            };

            btnStack.Children.Add(editBtn);
        }

        // Summarize button
        var summarizeBtn = new Button
        {
            Text = hasSummary ? "📋" : "📝",
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        SemanticProperties.SetHint(summarizeBtn, hasSummary ? "View Summary" : "Summarize document");

        summarizeBtn.Clicked += async (s, e) =>
        {
            if (hasSummary)
            {
                await ShowSummaryPopup(doc);
            }
            else
            {
                await SummarizeDocument(doc);
            }
        };

        btnStack.Children.Add(summarizeBtn);

        // Move to folder button
        var moveBtn = new Button
        {
            Text = "📁",
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        SemanticProperties.SetHint(moveBtn, "Move to folder");

        moveBtn.Clicked += async (s, e) =>
        {
            await ShowMoveToFolderPopup(doc);
        };

        btnStack.Children.Add(moveBtn);

        // Delete button
        var deleteBtn = new Button
        {
            Text = "🗑️",
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };

        deleteBtn.Clicked += async (s, e) =>
        {
            var confirm = await DisplayAlert("Delete", $"Delete '{doc.Title}'?", "Yes", "No");
            if (confirm)
            {
                await _documentService.DeleteDocumentAsync(doc.Id);
                _viewModel.Documents.Remove(doc);
                RefreshDocuments();
            }
        };

        btnStack.Children.Add(deleteBtn);

        Grid.SetColumn(btnStack, 2);

        grid.Children.Add(iconLabel);
        grid.Children.Add(contentStack);
        grid.Children.Add(btnStack);

        card.Content = grid;

        // Animate in
        _ = AnimateCardIn(card);

        return card;
    }

    private async Task AnimateCardIn(Frame card)
    {
        await Task.Delay(50);
        await Task.WhenAll(
            card.FadeTo(1, 200, Easing.CubicOut),
            card.TranslateTo(0, 0, 200, Easing.CubicOut)
        );
    }

    // ─── Folder Actions ───────────────────────────────────────────────────────────

    private async void OnNewFolderClicked(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync("New Folder", "Enter folder name:", "Create", "Cancel", "Folder name...", 50);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _viewModel.NewFolderName = name;
            await _viewModel.CreateFolderCommand.ExecuteAsync(null);
        }
    }

    private async void OnUncategorizedTapped(object sender, EventArgs e)
    {
        await _viewModel.ShowUncategorizedCommand.ExecuteAsync(null);
    }

    // ─── Document Actions ─────────────────────────────────────────────────────────

    private async void OnAddNoteClicked(object sender, EventArgs e)
    {
        await ShowAddNoteDialog();
    }

    private async void OnAddPdfClicked(object sender, EventArgs e)
    {
        try
        {
            // Request storage permission on Android
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.StorageRead>();
                }
            }

            var options = new PickOptions
            {
                PickerTitle = "Select a PDF file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/pdf" } },
                    { DevicePlatform.WinUI, new[] { ".pdf" } },
                    { DevicePlatform.iOS, new[] { "public.pdf" } },
                    { DevicePlatform.MacCatalyst, new[] { "pdf" } }
                })
            };

            var fileResult = await FilePicker.PickAsync(options);

            if (fileResult != null)
            {
                _viewModel.StatusMessage = "Processing PDF...";
                await _documentService.AddPdfAsync(fileResult.FileName, fileResult.FullPath);
                await _viewModel.InitializeAsync();
                RefreshFolders();
                RefreshDocuments();
                _viewModel.StatusMessage = "PDF imported successfully!";
            }
        }
        catch (PermissionException ex)
        {
            await DisplayAlert("Permission Required", $"Storage permission is needed to import PDFs. Please grant permission in Settings. ({ex.Message})", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to import PDF: {ex.Message}", "OK");
        }
    }

    private async void OnAddDocumentClicked(object sender, EventArgs e)
    {
        string action = await DisplayActionSheet(
            "Add Document",
            "Cancel",
            null,
            "📝 Add Note",
            "📄 Import PDF");

        if (action == "📝 Add Note")
        {
            await ShowAddNoteDialog();
        }
        else if (action == "📄 Import PDF")
        {
            OnAddPdfClicked(sender, e);
        }
    }

    private async Task ShowAddNoteDialog()
    {
        // Open the AI-powered note editor
        var editorPage = new NoteEditorPage(folderId: _viewModel.CurrentFolder?.Id);
        await Navigation.PushModalAsync(editorPage);

        // Refresh after editor closes
        await _viewModel.InitializeAsync();
        RefreshFolders();
        RefreshDocuments();
    }

    private async Task EditDocument(Document doc)
    {
        // Open the AI-powered note editor with existing document
        var editorPage = new NoteEditorPage(documentId: doc.Id);
        await Navigation.PushModalAsync(editorPage);

        // Refresh after editor closes
        await _viewModel.InitializeAsync();
        RefreshFolders();
        RefreshDocuments();
    }

    // ─── Smart Summarization ───────────────────────────────────────────────────────

    private async Task SummarizeDocument(Document doc)
    {
        _viewModel.StatusMessage = "Generating summary...";

        try
        {
            var summary = await _documentService.SummarizeDocumentAsync(doc.Id);

            // Update the document in the list
            doc.Summary = summary;
            doc.SummarizedAt = DateTime.UtcNow;

            // Refresh to show the summary indicator
            RefreshDocuments();
            _viewModel.StatusMessage = "Summary generated!";

            // Show the summary popup
            await ShowSummaryPopup(doc);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to generate summary: {ex.Message}", "OK");
            _viewModel.StatusMessage = "Summary failed.";
        }
    }

    private async Task ShowSummaryPopup(Document doc)
    {
        var summary = doc.Summary ?? "No summary available.";

        // Build popup content
        var popupContent = new Frame
        {
            CornerRadius = 20,
            Padding = new Thickness(20),
            BackgroundColor = Colors.White,
            HasShadow = true,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 320
        };

        var stack = new StackLayout { Spacing = 12 };

        // Header
        stack.Children.Add(new Label
        {
            Text = "📋 Summary",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#0078D4")
        });

        stack.Children.Add(new Label
        {
            Text = $"Key points from \"{doc.Title}\":",
            FontSize = 13,
            TextColor = Color.FromArgb("#666666")
        });

        // Summary content
        stack.Children.Add(new Label
        {
            Text = summary,
            FontSize = 14,
            TextColor = Colors.Black,
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Copy button
        var copyBtn = new Button
        {
            Text = "📋 Copy Summary",
            BackgroundColor = Color.FromArgb("#0078D4"),
            TextColor = Colors.White,
            CornerRadius = 10,
            HorizontalOptions = LayoutOptions.Fill
        };

        copyBtn.Clicked += async (s, e) =>
        {
            await Clipboard.SetTextAsync(summary);
            _viewModel.StatusMessage = "Summary copied!";
        };

        stack.Children.Add(copyBtn);

        // Close button
        var closeBtn = new Button
        {
            Text = "Close",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#666666"),
            CornerRadius = 10,
            HorizontalOptions = LayoutOptions.Fill
        };

        stack.Children.Add(closeBtn);

        popupContent.Content = stack;

        // Create popup page
        var popupPage = new ContentPage
        {
            Content = new Grid
            {
                BackgroundColor = Color.FromArgb("#80000000"),
                Children = { popupContent }
            }
        };

        closeBtn.Clicked += async (s, e) =>
        {
            await Navigation.PopModalAsync();
        };

        // Show popup
        await Navigation.PushModalAsync(popupPage);
    }

    // ─── Move to Folder ───────────────────────────────────────────────────────────

    private async Task ShowMoveToFolderPopup(Document doc)
    {
        var folders = await _folderService.GetAllFoldersAsync();

        var actionSheetButtons = new List<string> { "📄 Uncategorized" };
        actionSheetButtons.AddRange(folders.Select(f => $"{f.Icon ?? "📂"} {f.Name}"));

        var action = await DisplayActionSheet($"Move '{doc.Title}' to:", "Cancel", null, actionSheetButtons.ToArray());

        if (action == "📄 Uncategorized")
        {
            await _viewModel.MoveDocumentToFolderAsync(doc.Id, null);
            RefreshDocuments();
            RefreshFolders();
        }
        else if (!string.IsNullOrEmpty(action) && action != "Cancel")
        {
            var folderName = action.Substring(2).Trim(); // Remove emoji
            var targetFolder = folders.FirstOrDefault(f => f.Name == folderName);
            if (targetFolder != null)
            {
                await _viewModel.MoveDocumentToFolderAsync(doc.Id, targetFolder.Id);
                RefreshDocuments();
                RefreshFolders();
            }
        }
    }
}