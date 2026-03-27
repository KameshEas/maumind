using MauMind.App.Models;
using MauMind.App.Services;
using MauMind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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
    
    public ChatPage(ChatViewModel viewModel, IAnimationService animationService, IVoiceService? voiceService = null)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _animationService = animationService;
        _voiceService = voiceService;
        BindingContext = _viewModel;
        
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

            // Register messenger handlers for view events
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<ChatPage, MauMind.App.Messages.WebSearchRequestedMessage, string>(this, string.Empty, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() => ShowWebSearchCard(m.Value));
            });

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<ChatPage, MauMind.App.Messages.WebSearchDeniedMessage, string>(this, string.Empty, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(RemoveWebSearchCard);
            });

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<ChatPage, MauMind.App.Messages.FollowUpQuestionsMessage, string>(this, string.Empty, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() => ShowFollowUpChips(m.Value));
            });

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<ChatPage, MauMind.App.Messages.StreamingCompletedMessage, string>(this, string.Empty, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() => _ = BlinkOutCursorAsync());
            });
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unregister all messenger handlers for this view to avoid leaks
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(this);
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

            // Render provenance / sources for assistant messages
            if (!isUser && message.Provenance != null && message.Provenance.Count > 0)
            {
                var sourcesToggle = new Button
                {
                    Text = "Sources ▸",
                    FontSize = 12,
                    BackgroundColor = Colors.Transparent,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Start,
                    Padding = new Thickness(0)
                };

                var sourcesPanel = new StackLayout
                {
                    IsVisible = false,
                    Spacing = 6,
                    Margin = new Thickness(0, 6, 0, 0)
                };

                // Populate provenance entries
                foreach (var p in message.Provenance)
                {
                    var title = string.IsNullOrWhiteSpace(p.DocumentTitle) ? "(untitled)" : p.DocumentTitle;
                    var conf = Math.Round(p.Score, 3);

                    var itemFrame = new Frame
                    {
                        CornerRadius = 8,
                        Padding = new Thickness(10, 8),
                        BackgroundColor = Color.FromArgb("#F6F6F6"),
                        HasShadow = false
                    };

                    var itemStack = new StackLayout { Spacing = 6 };
                    var headerRow = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
                    headerRow.Children.Add(new Label { Text = $"{title}", FontSize = 12, TextColor = Colors.Black, FontAttributes = FontAttributes.Bold });
                    headerRow.Children.Add(new Label { Text = $"{p.Source}", FontSize = 11, TextColor = Colors.Gray });
                    headerRow.Children.Add(new Label { Text = $"({conf})", FontSize = 11, TextColor = Colors.Gray });

                    // Action buttons: open document, copy excerpt, share
                    var actionsRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.EndAndExpand };

                    var openBtn = new Button
                    {
                        Text = "📄",
                        FontSize = 14,
                        BackgroundColor = Colors.Transparent,
                        WidthRequest = 34,
                        HeightRequest = 34,
                        CornerRadius = 8
                    };
                    openBtn.Clicked += async (s, e) =>
                    {
                        try
                        {
                            if (p.DocumentId > 0)
                            {
                                var editor = ActivatorUtilities.CreateInstance<NoteEditorPage>(App.Services, p.DocumentId);
                                await Navigation.PushModalAsync(editor);
                            }
                            else
                            {
                                await DisplayAlert("Memory", "This source is from your memory (no document to open).", "OK");
                            }
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlert("Error", ex.Message, "OK");
                        }
                    };

                    var copyBtn = new Button
                    {
                        Text = "📋",
                        FontSize = 14,
                        BackgroundColor = Colors.Transparent,
                        WidthRequest = 34,
                        HeightRequest = 34,
                        CornerRadius = 8
                    };
                    copyBtn.Clicked += async (s, e) =>
                    {
                        try
                        {
                            await Clipboard.SetTextAsync(p.Excerpt ?? string.Empty);
                            await _animationService?.PulseAsync(copyBtn);
                        }
                        catch { }
                    };

                    var shareBtn = new Button
                    {
                        Text = "🔗",
                        FontSize = 14,
                        BackgroundColor = Colors.Transparent,
                        WidthRequest = 34,
                        HeightRequest = 34,
                        CornerRadius = 8
                    };
                    shareBtn.Clicked += async (s, e) =>
                    {
                        try
                        {
                            var share = App.Services.GetRequiredService<IShareService>();
                            var textToShare = $"{title}:\n\n{p.Excerpt}";
                            await share.ShareTextAsync(textToShare, title);
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlert("Share Error", ex.Message, "OK");
                        }
                    };

                    actionsRow.Children.Add(openBtn);
                    actionsRow.Children.Add(copyBtn);
                    actionsRow.Children.Add(shareBtn);

                    var excerpt = p.Excerpt ?? string.Empty;
                    itemStack.Children.Add(headerRow);
                    itemStack.Children.Add(new Label { Text = excerpt, FontSize = 12, TextColor = Colors.Gray, LineBreakMode = LineBreakMode.WordWrap });
                    itemStack.Children.Add(actionsRow);

                    itemFrame.Content = itemStack;
                    sourcesPanel.Children.Add(itemFrame);
                }

                sourcesToggle.Clicked += (s, e) =>
                {
                    sourcesPanel.IsVisible = !sourcesPanel.IsVisible;
                    sourcesToggle.Text = sourcesPanel.IsVisible ? "Sources ▾" : "Sources ▸";
                };

                stack.Children.Add(sourcesToggle);
                stack.Children.Add(sourcesPanel);
            }
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
        var settings = App.Services.GetRequiredService<SettingsPage>();
        await Shell.Current.Navigation.PushAsync(settings);
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

    // Web search handlers moved to messenger registrations

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

    // Follow-up handler moved to messenger registrations

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

    // Streaming completed handler moved to messenger registrations

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
