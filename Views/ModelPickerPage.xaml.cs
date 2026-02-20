using MauMind.App.Models;
using MauMind.App.Services;

namespace MauMind.App.Views;

public partial class ModelPickerPage : ContentPage
{
    private readonly IModelManager _modelManager;
    private CancellationTokenSource? _downloadCts;

    public ModelPickerPage()
    {
        InitializeComponent();
        _modelManager = App.GetService<IModelManager>();
        _modelManager.ModelChanged += OnModelChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RenderModelCards();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _modelManager.ModelChanged -= OnModelChanged;
    }

    private void OnModelChanged(object? sender, ModelInfo? model)
    {
        MainThread.BeginInvokeOnMainThread(RenderModelCards);
    }

    // ─── Build Model Cards ────────────────────────────────────────────────────

    private void RenderModelCards()
    {
        ModelsStack.Children.Clear();

        var models = _modelManager.AvailableModels;
        foreach (var model in models)
        {
            var card = BuildModelCard(model);
            ModelsStack.Children.Add(card);
        }
    }

    private View BuildModelCard(ModelInfo model)
    {
        bool isActive     = model.IsActive;
        bool isDownloaded = model.IsDownloaded;

        // Card frame
        var cardFrame = new Frame
        {
            CornerRadius    = 18,
            Padding         = new Thickness(16, 14),
            HasShadow       = isActive,
            BackgroundColor = isActive
                ? Color.FromArgb("#100078D4")
                : Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1E1E1E")
                    : Colors.White,
        };

        // Progress bar (hidden by default)
        var progressBar = new ProgressBar
        {
            Progress        = 0,
            IsVisible       = false,
            ProgressColor   = Color.FromArgb("#0078D4"),
            BackgroundColor = Color.FromArgb("#20000000"),
            HeightRequest   = 4,
            Margin          = new Thickness(0, 8, 0, 0),
        };

        // Status label (shows % during download)
        var statusLabel = new Label
        {
            Text      = string.Empty,
            FontSize  = 11,
            TextColor = Color.FromArgb("#6B6B6B"),
            IsVisible = false,
            Margin    = new Thickness(0, 2, 0, 0),
        };

        // Badge chip
        var badgeFrame = new Frame
        {
            CornerRadius    = 10,
            Padding         = new Thickness(8, 2),
            BackgroundColor = Color.FromArgb(model.BadgeColor + "20"),
            Margin          = new Thickness(0, 0, 8, 0),
        };
        badgeFrame.Content = new Label
        {
            Text      = model.Badge,
            FontSize  = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(model.BadgeColor),
        };

        // Action button
        var actionButton = BuildActionButton(model, isActive, isDownloaded);

        // Main layout
        var mainStack = new StackLayout { Spacing = 2 };

        // Top row: icon + name + badge
        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            }
        };

        // Icon circle
        var iconFrame = new Frame
        {
            WidthRequest    = 44,
            HeightRequest   = 44,
            CornerRadius    = 22,
            Padding         = 0,
            BackgroundColor = Color.FromArgb(model.BadgeColor + "20"),
        };
        iconFrame.Content = new Label
        {
            Text              = model.Icon,
            FontSize          = 22,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center,
        };

        var nameVersionStack = new StackLayout
        {
            Spacing = 2,
            Margin  = new Thickness(10, 0),
            VerticalOptions = LayoutOptions.Center,
        };
        nameVersionStack.Children.Add(new Label
        {
            Text           = model.DisplayName,
            FontSize       = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor      = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White : Color.FromArgb("#1A1A1A"),
        });
        nameVersionStack.Children.Add(new Label
        {
            Text      = $"{model.Version} · {model.FileSizeDisplay}",
            FontSize  = 12,
            TextColor = Color.FromArgb("#6B6B6B"),
        });

        Grid.SetColumn(iconFrame, 0);
        Grid.SetColumn(nameVersionStack, 1);
        Grid.SetColumn(badgeFrame, 2);

        topRow.Children.Add(iconFrame);
        topRow.Children.Add(nameVersionStack);
        topRow.Children.Add(badgeFrame);

        // Description
        var descLabel = new Label
        {
            Text      = model.Description,
            FontSize  = 13,
            TextColor = Color.FromArgb("#6B6B6B"),
            Margin    = new Thickness(0, 8, 0, 0),
            LineHeight = 1.4,
        };

        mainStack.Children.Add(topRow);
        mainStack.Children.Add(descLabel);
        mainStack.Children.Add(progressBar);
        mainStack.Children.Add(statusLabel);
        mainStack.Children.Add(actionButton);

        cardFrame.Content = mainStack;

        // Wire up button logic
        WireButtonAction(actionButton, model, progressBar, statusLabel, cardFrame);

        return cardFrame;
    }

    private Button BuildActionButton(ModelInfo model, bool isActive, bool isDownloaded)
    {
        if (isActive)
        {
            return new Button
            {
                Text            = "✓ Active",
                BackgroundColor = Color.FromArgb("#0078D4"),
                TextColor       = Colors.White,
                FontAttributes  = FontAttributes.Bold,
                CornerRadius    = 20,
                HeightRequest   = 40,
                HorizontalOptions = LayoutOptions.Fill,
                Margin          = new Thickness(0, 10, 0, 0),
                IsEnabled       = false,
            };
        }

        if (isDownloaded)
        {
            return new Button
            {
                Text            = "Switch to this model",
                BackgroundColor = Colors.Transparent,
                TextColor       = Color.FromArgb("#0078D4"),
                FontAttributes  = FontAttributes.Bold,
                CornerRadius    = 20,
                HeightRequest   = 40,
                BorderColor     = Color.FromArgb("#0078D4"),
                BorderWidth     = 1.5,
                HorizontalOptions = LayoutOptions.Fill,
                Margin          = new Thickness(0, 10, 0, 0),
            };
        }

        return new Button
        {
            Text            = $"Download · {model.FileSizeDisplay}",
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            TextColor       = Color.FromArgb("#1A1A1A"),
            FontAttributes  = FontAttributes.Bold,
            CornerRadius    = 20,
            HeightRequest   = 40,
            HorizontalOptions = LayoutOptions.Fill,
            Margin          = new Thickness(0, 10, 0, 0),
        };
    }

    private void WireButtonAction(Button btn, ModelInfo model,
        ProgressBar progressBar, Label statusLabel, Frame card)
    {
        btn.Clicked += async (s, e) =>
        {
            if (model.IsActive) return;

            if (model.IsDownloaded)
            {
                // Switch immediately
                btn.IsEnabled = false;
                btn.Text      = "Switching...";

                bool ok = await _modelManager.SwitchModelAsync(model.Id);

                if (ok)
                {
                    await DisplayAlert("Model Switched",
                        $"{model.DisplayName} is now your active AI model.", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    btn.IsEnabled = true;
                    btn.Text      = "Switch to this model";
                    await DisplayAlert("Error", "Failed to switch model.", "OK");
                }
            }
            else
            {
                // Download flow
                bool confirm = await DisplayAlert(
                    $"Download {model.DisplayName}",
                    $"This will download {model.FileSizeDisplay}. Make sure you are on Wi-Fi.",
                    "Download", "Cancel");

                if (!confirm) return;

                // Show progress
                progressBar.IsVisible = true;
                statusLabel.IsVisible = true;
                btn.IsEnabled         = false;
                btn.Text              = "Downloading...";

                _downloadCts = new CancellationTokenSource();

                var progress = new Progress<double>(p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        progressBar.Progress = p;
                        statusLabel.Text     = $"{p * 100:0}% downloaded";
                    });
                });

                bool downloaded = await _modelManager.DownloadModelAsync(
                    model.Id, progress, _downloadCts.Token);

                progressBar.IsVisible = false;
                statusLabel.IsVisible = false;

                if (downloaded)
                {
                    // Auto-switch after download
                    bool switched = await _modelManager.SwitchModelAsync(model.Id);
                    if (switched)
                    {
                        await DisplayAlert("Ready!",
                            $"{model.DisplayName} downloaded and activated.", "OK");
                        await Navigation.PopAsync();
                    }
                }
                else
                {
                    btn.IsEnabled = true;
                    btn.Text      = $"Download · {model.FileSizeDisplay}";
                    await DisplayAlert("Download Failed",
                        "Could not download the model. Check your connection.", "OK");
                }
            }
        };
    }
}
