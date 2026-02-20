using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauMind.App.Models;
using MauMind.App.Services;
using System.Collections.ObjectModel;

namespace MauMind.App.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chatService;
    private readonly IVectorStore _vectorStore;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly IWebSearchService _webSearchService;
    private readonly IFollowUpService _followUpService;
    private CancellationTokenSource? _cts;

    // Web search confirmation state
    [ObservableProperty] private bool _isWebSearchPending;
    [ObservableProperty] private string _pendingWebSearchQuery = string.Empty;

    // Follow-up question chips (3 after each AI reply)
    [ObservableProperty] private List<string> _followUpQuestions = new();
    
    // Track previous follow-ups to prevent duplicates
    private HashSet<string> _previousFollowUps = new();

    // Last user query (for follow-up generation)
    private string _lastUserQuery = string.Empty;

    // Event to notify view to render web confirmation card
    public event EventHandler<string>? WebSearchConfirmationRequested;
    public event EventHandler? WebSearchDenied;

    // Event to notify view to refresh follow-up chips
    public event EventHandler<List<string>>? FollowUpQuestionsReady;

    // Event fired when streaming finishes — view does cursor blink-out
    public event EventHandler? StreamingCompleted;
    
    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();
    
    [ObservableProperty]
    private string _userInput = string.Empty;
    
    [ObservableProperty]
    private bool _isProcessing;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    [ObservableProperty]
    private bool _isModelsLoaded;
    
    [ObservableProperty]
    private bool _hasError;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    public ChatViewModel(IChatService chatService, IVectorStore vectorStore,
        IErrorHandlingService errorHandlingService, IWebSearchService webSearchService,
        IFollowUpService followUpService)
    {
        _chatService = chatService;
        _vectorStore = vectorStore;
        _errorHandlingService = errorHandlingService;
        _webSearchService = webSearchService;
        _followUpService = followUpService;
    }
    
    public async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "Loading AI models...";
            
            // Initialize vector store (loads embedding model)
            await _vectorStore.InitializeAsync();
            
            // Load chat model
            await _chatService.LoadModelAsync();
            
            IsModelsLoaded = true;
            StatusMessage = "Ready";
            
            // Load chat history
            var history = await _chatService.GetChatHistoryAsync();
            foreach (var message in history)
            {
                Messages.Add(message);
            }
            
            // Add welcome message if no history
            if (Messages.Count == 0)
            {
                var welcomeMessage = new ChatMessage
                {
                    Content = "Hello! I'm MauMind, your offline personal AI assistant. I've been designed with privacy at my core - all your data stays on your device.\n\nYou can ask me questions about your documents, or just have a conversation. Try adding some notes or PDFs through the Documents tab first!",
                    IsUser = false,
                    Timestamp = DateTime.UtcNow
                };
                Messages.Add(welcomeMessage);
                await _chatService.SaveMessageAsync(welcomeMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsProcessing)
            return;
        
        var userMessage = UserInput.Trim();
        UserInput = string.Empty;
        
        // Add user message to chat
        var userMsg = new ChatMessage
        {
            Content = userMessage,
            IsUser = true,
            Timestamp = DateTime.UtcNow
        };
        Messages.Add(userMsg);
        await _chatService.SaveMessageAsync(userMsg);
        
        // Process response
        await GenerateResponse(userMessage);
    }
    
    private async Task GenerateResponse(string userMessage)
    {
        IsProcessing = true;
        HasError = false;
        ErrorMessage = string.Empty;
        _cts = new CancellationTokenSource();
        
        try
        {
            StatusMessage = "Searching documents...";
            
            // Add timeout for the entire response generation
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
            
            // Start a task to monitor for timeout
            var timeoutTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), linkedCts.Token);
                    if (!linkedCts.IsCancellationRequested)
                    {
                        // Timeout occurred
                        StatusMessage = "Taking too long... Try a simpler question";
                    }
                }
                catch (OperationCanceledException) { }
            });
            
            // Create assistant message placeholder
            var assistantMsg = new ChatMessage
            {
                Content = string.Empty,
                IsUser = false,
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(assistantMsg);
            
            // Stream response with batched UI updates (80ms) and cursor
            var responseBuilder  = new System.Text.StringBuilder();
            var pendingTokens    = new System.Text.StringBuilder();
            bool webSearchNeeded = false;
            var lastUiUpdate     = DateTime.UtcNow;
            const int UiBatchMs  = 80;

            void FlushToUI(bool withCursor)
            {
                if (pendingTokens.Length == 0 && !withCursor) return;
                responseBuilder.Append(pendingTokens);
                pendingTokens.Clear();
                var display = withCursor
                    ? responseBuilder + "▋"
                    : responseBuilder.ToString();
                assistantMsg.Content = display;
                var idx = Messages.Count - 1;
                Messages.RemoveAt(idx);
                Messages.Add(assistantMsg);
            }

            await foreach (var token in _chatService.GetStreamingResponseAsync(userMessage, _cts.Token))
            {
                if (token == "__NEEDS_WEB_SEARCH__")
                {
                    webSearchNeeded = true;
                    break;
                }

                pendingTokens.Append(token);

                // Flush UI every 80ms to keep it smooth without hammering
                if ((DateTime.UtcNow - lastUiUpdate).TotalMilliseconds >= UiBatchMs)
                {
                    FlushToUI(withCursor: true);
                    lastUiUpdate = DateTime.UtcNow;
                }
            }

            if (webSearchNeeded)
            {
                // Remove empty placeholder message
                if (Messages.Count > 0 && !Messages[^1].IsUser && string.IsNullOrEmpty(Messages[^1].Content))
                    Messages.RemoveAt(Messages.Count - 1);

                IsProcessing = false;
                RequestWebSearch(userMessage);
                return;
            }

            // Final flush without cursor — clean text
            FlushToUI(withCursor: false);
            assistantMsg.Content = responseBuilder.ToString();

            // Notify view to do cursor blink-out animation
            StreamingCompleted?.Invoke(this, EventArgs.Empty);
            
            // Save assistant message
            await _chatService.SaveMessageAsync(assistantMsg);

            // Generate follow-up questions in background
            _ = Task.Run(() =>
            {
                var followUps = _followUpService.GenerateFollowUps(userMessage, assistantMsg.Content);
                FollowUpQuestions = followUps;
                FollowUpQuestionsReady?.Invoke(this, followUps);
            });

            StatusMessage = "Ready";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Response cancelled";
        }
        catch (Exception ex)
        {
            // Use user-friendly error message
            var friendlyMessage = _errorHandlingService.GetUserFriendlyMessage(ex);
            StatusMessage = $"Error: {friendlyMessage}";
            HasError = true;
            ErrorMessage = friendlyMessage;
            
            // Add user-friendly error message to chat
            var errorMsg = new ChatMessage
            {
                Content = $"I apologize, but I encountered an issue: {friendlyMessage}\n\nPlease try again or check the Settings.",
                IsUser = false,
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(errorMsg);
            
            // Log the actual error
            _errorHandlingService.HandleError(ex, "ChatViewModel.GenerateResponse");
        }
        finally
        {
            IsProcessing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }
    
    [RelayCommand]
    private void CancelResponse()
    {
        _cts?.Cancel();
    }
    
    [RelayCommand]
    private async Task ClearChat()
    {
        Messages.Clear();
        await _chatService.ClearHistoryAsync();
        
        // Add welcome message
        var welcomeMessage = new ChatMessage
        {
            Content = "Chat cleared. How can I help you?",
            IsUser = false,
            Timestamp = DateTime.UtcNow
        };
        Messages.Add(welcomeMessage);
    }

    // ─── Web Search ───────────────────────────────────────────────────────────

    /// <summary>Called by ChatService when no local data found. Triggers confirmation UI.</summary>
    public void RequestWebSearch(string query)
    {
        if (!_webSearchService.IsEnabled) return;
        PendingWebSearchQuery = query;
        IsWebSearchPending = true;
        WebSearchConfirmationRequested?.Invoke(this, query);
    }

    /// <summary>User confirmed: proceed with web search.</summary>
    [RelayCommand]
    public async Task ConfirmWebSearch()
    {
        if (!IsWebSearchPending) return;

        var query = PendingWebSearchQuery;
        IsWebSearchPending = false;
        IsProcessing = true;
        StatusMessage = "Searching the web...";

        try
        {
            var searchResult = await _webSearchService.SearchAsync(query);

            if (!searchResult.IsSuccess || searchResult.Results.Count == 0)
            {
                var noResultMsg = new ChatMessage
                {
                    Content = $"🌐 Sorry, I couldn't find web results for \"{query}\". {searchResult.ErrorMessage ?? "No results found."}",
                    IsUser = false,
                    Timestamp = DateTime.UtcNow
                };
                Messages.Add(noResultMsg);
                await _chatService.SaveMessageAsync(noResultMsg);
                return;
            }

            // Build context from web results
            var context = WebSearchService.FormatResultsAsContext(searchResult);

            // Stream answer using local model
            var assistantMsg = new ChatMessage
            {
                Content = "🌐 **Web result:**\n\n",
                IsUser = false,
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(assistantMsg);

            // Append each snippet
            foreach (var result in searchResult.Results.Take(2))
            {
                assistantMsg.Content += $"**{result.Title}**\n{result.Snippet}\n\n";
                var idx = Messages.Count - 1;
                Messages.RemoveAt(idx);
                Messages.Add(assistantMsg);
                await Task.Delay(30); // brief streaming effect
            }

            await _chatService.SaveMessageAsync(assistantMsg);
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            StatusMessage = "Web search failed";
            var errMsg = new ChatMessage
            {
                Content = $"🌐 Web search failed: {ex.Message}",
                IsUser = false,
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(errMsg);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>User declined: show a helpful fallback message.</summary>
    [RelayCommand]
    public void DenyWebSearch()
    {
        IsWebSearchPending = false;
        PendingWebSearchQuery = string.Empty;
        WebSearchDenied?.Invoke(this, EventArgs.Empty);
    }
}
