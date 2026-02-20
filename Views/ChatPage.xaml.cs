using MauMind.App.Models;
using MauMind.App.Services;
using MauMind.App.ViewModels;
using Microsoft.Maui.Controls;

namespace MauMind.App.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;
    private readonly IAnimationService _animationService;
    private IVoiceService? _voiceService;
    private bool _isListening;
    private bool _isInitialized;
    private string _searchQuery = string.Empty;
    private List<ChatMessage> _allMessages = new();
    
    public ChatPage()
    {
        InitializeComponent();
        
        _viewModel = App.GetService<ChatViewModel>();
        _animationService = App.GetService<IAnimationService>();
        BindingContext = _viewModel;
        
        try
        {
            _voiceService = App.GetService<IVoiceService>();
        }
        catch { }
        
        Loaded += async (s, e) => 
        {
            // Only initialize once (singleton pattern)
            if (!_isInitialized)
            {
                _isInitialized = true;
                await _viewModel.InitializeAsync();
            }
            RefreshMessages();
            _viewModel.Messages.CollectionChanged += (sender, args) => RefreshMessages();
            
            // Add press animations to buttons
            _animationService?.AddPressAnimation(SendButton);
            _animationService?.AddPressAnimation(VoiceButton);

            // Subscribe to web search events
            _viewModel.WebSearchConfirmationRequested += OnWebSearchConfirmationRequested;
            _viewModel.WebSearchDenied += OnWebSearchDenied;

            // Subscribe to follow-up questions
            _viewModel.FollowUpQuestionsReady += OnFollowUpQuestionsReady;

            // Subscribe to streaming completed (cursor blink-out)
            _viewModel.StreamingCompleted += OnStreamingCompleted;
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.WebSearchConfirmationRequested -= OnWebSearchConfirmationRequested;
        _viewModel.WebSearchDenied -= OnWebSearchDenied;
        _viewModel.FollowUpQuestionsReady -= OnFollowUpQuestionsReady;
        _viewModel.StreamingCompleted -= OnStreamingCompleted;
    }
    
    private async void RefreshMessages()
    {
        MessagesStack.Children.Clear();
        
        // Store all messages
        _allMessages = _viewModel.Messages.ToList();
        
        // Filter by search if needed
        var messagesToShow = string.IsNullOrEmpty(_searchQuery) 
            ? _allMessages 
            : _allMessages.Where(m => m.Content.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        
        int index = 0;
        foreach (var message in messagesToShow)
        {
            var isUser = message.IsUser;
            
            // Create message bubble
            var bubble = new Frame
            {
                CornerRadius = 18,
                Padding = new Thickness(14, 10, 14, 10),
                HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = 320,
                BackgroundColor = isUser ? Color.FromRgb(0, 120, 212) : Colors.White,
                HasShadow = false,
                Opacity = 0,
                TranslationY = 20
            };
            
            var stack = new StackLayout();
            
            // Message content with copy button
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var contentLabel = new Label
            {
                Text = message.Content,
                TextColor = isUser ? Colors.White : Colors.Black,
                FontSize = 15,
                LineBreakMode = LineBreakMode.WordWrap,
                VerticalOptions = LayoutOptions.Start
            };
            Grid.SetColumn(contentLabel, 0);
            
            // Copy button (for AI messages)
            if (!isUser)
            {
                var copyButton = new Button
                {
                    Text = "📋",
                    FontSize = 12,
                    BackgroundColor = Colors.Transparent,
                    WidthRequest = 30,
                    HeightRequest = 30,
                    Padding = 0,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.End
                };
                copyButton.Clicked += async (s, e) =>
                {
                    await Clipboard.SetTextAsync(message.Content);
                    
                    // Pulse animation on copy
                    if (_animationService != null)
                    {
                        await _animationService.PulseAsync(copyButton);
                    }
                    
                    // Show brief toast-like message (using DisplayAlert briefly)
                    await DisplayAlert("Copied", "Message copied!", "OK");
                };
                Grid.SetColumn(copyButton, 1);
                contentGrid.Children.Add(contentLabel);
                contentGrid.Children.Add(copyButton);
            }
            else
            {
                contentGrid.Children.Add(contentLabel);
            }
            
            // Timestamp and status
            var footerStack = new StackLayout 
            { 
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,
                Margin = new Thickness(0, 5, 0, 0)
            };
            
            var timeLabel = new Label
            {
                Text = message.Timestamp.ToString("HH:mm"),
                FontSize = 10,
                TextColor = isUser ? Colors.White.WithAlpha(0.7f) : Colors.Gray
            };
            footerStack.Children.Add(timeLabel);
            
            stack.Children.Add(contentGrid);
            stack.Children.Add(footerStack);
            bubble.Content = stack;
            
            MessagesStack.Children.Add(bubble);
            
            // Animate message appearance with stagger
            _ = AnimateMessage(bubble, index * 50);
            index++;
        }
    }
    
    private async Task AnimateMessage(Frame bubble, int delay)
    {
        await Task.Delay(delay);
        await Task.WhenAll(
            bubble.FadeTo(1, 200),
            bubble.TranslateTo(0, 0, 200, Easing.CubicOut)
        );
    }
    
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = e.NewTextValue ?? string.Empty;
        RefreshMessages();
    }
    
    private void OnClearSearchClicked(object sender, EventArgs e)
    {
        _searchQuery = string.Empty;
        RefreshMessages();
    }
    
    private async void OnSendClicked(object sender, EventArgs e)
    {
        // Pulse animation on send button
        if (_animationService != null)
        {
            await _animationService.PulseAsync(SendButton);
        }
        await SendMessage();
    }
    
    private async void OnVoiceClicked(object sender, EventArgs e)
    {
        if (_voiceService == null)
        {
            await DisplayAlert("Voice", "Voice service not available", "OK");
            return;
        }
        
        if (_isListening) return;
        
        _isListening = true;
        
        // Pulse animation on voice button
        if (_animationService != null)
        {
            await _animationService.PulseAsync(VoiceButton);
        }
        
        VoiceButton.Text = "🔴";
        
        try
        {
            var result = await _voiceService.ListenAsync();
            
            if (!string.IsNullOrEmpty(result) && result != "Listening... (speak now)")
            {
                MessageEntry.Text = result;
            }
            else
            {
                await DisplayAlert("Voice Input", "Speak now. Your voice will appear in the text box. Then tap Send.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Voice Error", ex.Message, "OK");
        }
        finally
        {
            _isListening = false;
            VoiceButton.Text = "🎤";
        }
    }
    
    private async void OnSampleQuestionClicked(object sender, EventArgs e)
    {
        // Pulse animation on sample button
        if (_animationService != null)
        {
            await _animationService.PulseAsync((Button)sender);
        }
        
        MessageEntry.Text = "What documents do I have?";
        await SendMessage();
    }
    
    private void OnCancelClicked(object sender, EventArgs e)
    {
        _viewModel.CancelResponseCommand.Execute(null);
    }
    
    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new SettingsPage());
    }
    
    private async Task SendMessage()
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        
        MessageEntry.Text = string.Empty;
        
        if (_viewModel.SendMessageCommand.CanExecute(null))
        {
            _viewModel.UserInput = text;
            await _viewModel.SendMessageCommand.ExecuteAsync(null);
        }
    }

    // ─── Web Search Confirmation Card ─────────────────────────────────────────

    private Frame? _webSearchCard;

    private void OnWebSearchConfirmationRequested(object? sender, string query)
    {
        MainThread.BeginInvokeOnMainThread(() => ShowWebSearchCard(query));
    }

    private void OnWebSearchDenied(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RemoveWebSearchCard);
    }

    private void ShowWebSearchCard(string query)
    {
        RemoveWebSearchCard(); // remove any existing

        // Outer card
        var card = new Frame
        {
            CornerRadius    = 18,
            Padding         = new Thickness(16, 14),
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Color.FromArgb("#FFF8E7"),
            HasShadow       = true,
            Opacity         = 0,
            TranslationY    = 16,
        };

        // Icon + title row
        var titleRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        titleRow.Children.Add(new Label { Text = "💡", FontSize = 20, VerticalOptions = LayoutOptions.Center });
        titleRow.Children.Add(new Label
        {
            Text           = "No local data found",
            FontSize       = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor      = Color.FromArgb("#1A1A1A"),
            VerticalOptions = LayoutOptions.Center,
        });

        // Description
        var desc = new Label
        {
            Text      = $"I didn't find anything in your documents about:\n\"{query}\"\n\nShall I search the web? (DuckDuckGo, privacy-first)",
            FontSize  = 13,
            TextColor = Color.FromArgb("#4A4A4A"),
            LineHeight = 1.4,
            Margin    = new Thickness(0, 0, 0, 12),
        };

        // Button row
        var btnRow = new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Fill };

        var searchBtn = new Button
        {
            Text            = "🌐 Search Web",
            BackgroundColor = Color.FromArgb("#0078D4"),
            TextColor       = Colors.White,
            FontAttributes  = FontAttributes.Bold,
            CornerRadius    = 20,
            HeightRequest   = 40,
            Padding         = new Thickness(16, 0),
        };
        searchBtn.Clicked += async (s, e) =>
        {
            RemoveWebSearchCard();
            await _viewModel.ConfirmWebSearchCommand.ExecuteAsync(null);
        };

        var skipBtn = new Button
        {
            Text            = "✕ Skip",
            BackgroundColor = Colors.Transparent,
            TextColor       = Color.FromArgb("#888"),
            CornerRadius    = 20,
            HeightRequest   = 40,
            Padding         = new Thickness(12, 0),
            BorderColor     = Color.FromArgb("#DDD"),
            BorderWidth     = 1,
        };
        skipBtn.Clicked += (s, e) =>
        {
            RemoveWebSearchCard();
            _viewModel.DenyWebSearchCommand.Execute(null);
        };

        btnRow.Children.Add(searchBtn);
        btnRow.Children.Add(skipBtn);

        var content = new StackLayout { Spacing = 0 };
        content.Children.Add(titleRow);
        content.Children.Add(desc);
        content.Children.Add(btnRow);

        card.Content = content;

        _webSearchCard = card;
        MessagesStack.Children.Add(card);

        // Animate in
        _ = Task.WhenAll(
            card.FadeTo(1, 250, Easing.CubicOut),
            card.TranslateTo(0, 0, 250, Easing.CubicOut)
        );
    }

    private void RemoveWebSearchCard()
    {
        if (_webSearchCard != null)
        {
            MessagesStack.Children.Remove(_webSearchCard);
            _webSearchCard = null;
        }
    }

    // ─── Follow-Up Question Chips ─────────────────────────────────────────────

    private StackLayout? _followUpChipsRow;

    private void OnFollowUpQuestionsReady(object? sender, List<string> questions)
    {
        MainThread.BeginInvokeOnMainThread(() => ShowFollowUpChips(questions));
    }

    private void ShowFollowUpChips(List<string> questions)
    {
        RemoveFollowUpChips();

        if (questions == null || questions.Count == 0) return;

        // Container
        var container = new StackLayout
        {
            Spacing  = 0,
            Margin   = new Thickness(0, 4, 0, 8),
            Opacity  = 0,
            TranslationY = 12,
        };

        // Label row
        container.Children.Add(new Label
        {
            Text      = "💬 Related questions",
            FontSize  = 12,
            TextColor = Color.FromArgb("#888888"),
            Margin    = new Thickness(4, 0, 0, 6),
        });

        // Chip stack
        var chipStack = new StackLayout { Spacing = 6 };

        foreach (var question in questions)
        {
            var chip = BuildChip(question);
            chipStack.Children.Add(chip);
        }

        container.Children.Add(chipStack);

        _followUpChipsRow = container;
        MessagesStack.Children.Add(container);

        // Animate chips in with stagger
        _ = AnimateChipsIn(container, chipStack);
    }

    private Frame BuildChip(string question)
    {
        var chip = new Frame
        {
            CornerRadius    = 20,
            Padding         = new Thickness(14, 8),
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Colors.White,
            HasShadow       = false,
            BorderColor     = Color.FromArgb("#D0E8FF"),
        };

        var label = new Label
        {
            Text      = question,
            FontSize  = 13,
            TextColor = Color.FromArgb("#0078D4"),
            LineBreakMode = LineBreakMode.WordWrap,
        };

        chip.Content = label;

        // Tap to send
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) =>
        {
            RemoveFollowUpChips();
            MessageEntry.Text = question;
            await SendMessage();
        };
        chip.GestureRecognizers.Add(tapGesture);

        return chip;
    }

    private async Task AnimateChipsIn(StackLayout container, StackLayout chipStack)
    {
        await Task.WhenAll(
            container.FadeTo(1, 200, Easing.CubicOut),
            container.TranslateTo(0, 0, 200, Easing.CubicOut)
        );

        // Stagger each chip
        int delay = 0;
        foreach (var chip in chipStack.Children.OfType<Frame>())
        {
            chip.Opacity     = 0;
            chip.TranslationX = -16;
            _ = Task.WhenAll(
                chip.FadeTo(1, 180, Easing.CubicOut),
                chip.TranslateTo(0, 0, 180, Easing.CubicOut)
            );
            await Task.Delay(80);
            delay += 80;
        }
    }

    private void RemoveFollowUpChips()
    {
        if (_followUpChipsRow != null)
        {
            MessagesStack.Children.Remove(_followUpChipsRow);
            _followUpChipsRow = null;
        }
    }

    // ─── Streaming Cursor Blink-Out ───────────────────────────────────────────

    private void OnStreamingCompleted(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = BlinkOutCursorAsync());
    }

    private async Task BlinkOutCursorAsync()
    {
        try
        {
            // Find the last AI message in the stack - using safer approach
            var lastAiBubble = MessagesStack.Children
                .OfType<Frame>()
                .LastOrDefault(f => f.BackgroundColor == Colors.White);

            if (lastAiBubble?.Content is not StackLayout sl) return;

            var grid = sl.Children.OfType<Grid>().FirstOrDefault();
            if (grid == null) return;

            var label = grid.Children.OfType<Label>().FirstOrDefault();
            if (label == null || string.IsNullOrEmpty(label.Text)) return;

            var text = label.Text ?? string.Empty;
            var finalText = text.TrimEnd('▋').TrimEnd();

            // Blink cursor 3 times then settle on clean text
            for (int i = 0; i < 3; i++)
            {
                label.Text = finalText + "▋";
                await Task.Delay(250);
                label.Text = finalText;
                await Task.Delay(200);
            }

            // Ensure clean final state
            label.Text = finalText;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in BlinkOutCursorAsync: {ex.Message}");
        }
    }
}
