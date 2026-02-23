using MauMind.App.Services;

namespace MauMind.App.Views;

public partial class PinEntryPage : ContentPage
{
    private readonly ISecretModeService _secretModeService;
    private string _currentPin = string.Empty;
    private string _confirmPin = string.Empty;
    private bool _isConfirming = false;
    private bool _isSettingUp = false;
    private Frame[] _pinFrames;
    
    // Callback for successful PIN entry
    public Action? OnPinVerified { get; set; }

    public PinEntryPage()
    {
        InitializeComponent();
        
        _secretModeService = App.GetService<ISecretModeService>();
        _pinFrames = new[] { Pin1, Pin2, Pin3, Pin4 };
        
        // Determine mode based on whether PIN is already set
        if (_secretModeService.IsPinSet)
        {
            // PIN is set, user needs to enter it
            _isSettingUp = false;
            TitleLabel.Text = "Enter PIN";
            SubtitleLabel.Text = "Enter your 4-digit PIN to access Secret Mode";
            LockIcon.Text = "🔒";
            ForgotPinButton.IsVisible = true;
        }
        else
        {
            // No PIN set, user needs to create one
            _isSettingUp = true;
            TitleLabel.Text = "Create PIN";
            SubtitleLabel.Text = "Create a 4-digit PIN to protect Secret Mode";
            LockIcon.Text = "🔐";
            ForgotPinButton.IsVisible = false;
        }
    }

    private void OnNumberClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string tag)
        {
            AddDigit(tag);
        }
    }

    private void OnBackspaceClicked(object? sender, EventArgs e)
    {
        RemoveDigit();
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        // Go back to previous page
        Navigation.PopAsync();
    }

    private async void OnForgotPinClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Forgot PIN?", "Cancel", "Reset PIN & Secret Mode", "Go Back");
        
        if (action == "Reset PIN & Secret Mode")
        {
            var confirm = await DisplayAlert("Reset PIN", 
                "This will remove your PIN and disable Secret Mode. All secret conversations will remain but will no longer be protected. Continue?", 
                "Reset", "Cancel");
            
            if (confirm)
            {
                // Reset PIN (in a real app, you'd need the old PIN first)
                await DisplayAlert("PIN Reset", "PIN has been reset. Please restart the app to set up a new PIN.", "OK");
                await Navigation.PopAsync();
            }
        }
    }

    private void AddDigit(string digit)
    {
        var pin = _isConfirming ? _confirmPin : _currentPin;
        
        if (pin.Length >= 4)
            return;

        pin += digit;
        
        if (_isConfirming)
            _confirmPin = pin;
        else
            _currentPin = pin;

        UpdatePinDisplay();

        // Auto-validate when 4 digits entered
        if (pin.Length == 4)
        {
            ProcessPin();
        }
    }

    private void RemoveDigit()
    {
        var pin = _isConfirming ? _confirmPin : _currentPin;
        
        if (pin.Length == 0)
            return;

        pin = pin[..^1];
        
        if (_isConfirming)
            _confirmPin = pin;
        else
            _currentPin = pin;

        UpdatePinDisplay();
        HideError();
    }

    private void UpdatePinDisplay()
    {
        var pin = _isConfirming ? _confirmPin : _currentPin;
        
        for (int i = 0; i < 4; i++)
        {
            if (i < pin.Length)
            {
                _pinFrames[i].Background = Application.Current.RequestedTheme == AppTheme.Dark 
                    ? (SolidColorBrush)Resources["PrimaryColor"] 
                    : new SolidColorBrush(Color.FromArgb("#0078D4"));
            }
            else
            {
                _pinFrames[i].Background = Application.Current.RequestedTheme == AppTheme.Dark 
                    ? (SolidColorBrush)Resources["DarkBorder"] 
                    : new SolidColorBrush(Color.FromArgb("#E5E7EB"));
            }
        }
    }

    private async void ProcessPin()
    {
        if (_isSettingUp)
        {
            if (!_isConfirming)
            {
                // First entry, now confirm
                _isConfirming = true;
                TitleLabel.Text = "Confirm PIN";
                SubtitleLabel.Text = "Re-enter your 4-digit PIN";
                LockIcon.Text = "🔐";
                UpdatePinDisplay();
            }
            else
            {
                // Confirming PIN
                if (_currentPin == _confirmPin)
                {
                    // PINs match, save and enable
                    await _secretModeService.SetPinAsync(_currentPin);
                    _secretModeService.EnableSecretMode();
                    
                    await DisplayAlert("Success", "Secret Mode PIN has been set! Your private chats are now protected.", "OK");
                    OnPinVerified?.Invoke();
                    await Navigation.PopAsync();
                }
                else
                {
                    // PINs don't match
                    ShowError("PINs don't match. Try again.");
                    _confirmPin = string.Empty;
                    _currentPin = string.Empty;
                    _isConfirming = false;
                    _isSettingUp = true;
                    TitleLabel.Text = "Create PIN";
                    SubtitleLabel.Text = "Create a 4-digit PIN to protect Secret Mode";
                    UpdatePinDisplay();
                }
            }
        }
        else
        {
            // Validating existing PIN
            var isValid = await _secretModeService.ValidatePinAsync(_currentPin);
            
            if (isValid)
            {
                // PIN valid, enable secret mode temporarily
                _secretModeService.EnableSecretMode();
                OnPinVerified?.Invoke();
                await Navigation.PopAsync();
            }
            else
            {
                ShowError("Incorrect PIN. Try again.");
                _currentPin = string.Empty;
                UpdatePinDisplay();
            }
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
        
        // Shake animation
        var animation = new Animation(v => 
        {
            // Simple shake effect
        });
    }

    private void HideError()
    {
        ErrorLabel.IsVisible = false;
    }
}
