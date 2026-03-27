using System.Text.Json;
using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IAuthService
{
    AuthState CurrentState { get; }
    event EventHandler<AuthState>? AuthStateChanged;

    /// <summary>Sign in an existing account.</summary>
    Task<bool> LoginAsync(string email, string password);

    /// <summary>
    /// Register a new account.  Returns (true, null) on success,
    /// or (false, humanReadableError) on failure.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> SignUpAsync(string email, string password);

    Task LogoutAsync();

    /// <summary>Silently refresh the auth token/session. No-op when offline or unconfigured.</summary>
    Task RefreshSessionAsync();
}

public class AuthService : IAuthService
{
    private readonly string _statePath;

    public AuthState CurrentState { get; private set; } = new();

    public event EventHandler<AuthState>? AuthStateChanged;

    public AuthService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind");

        Directory.CreateDirectory(appDataPath);
        _statePath = Path.Combine(appDataPath, "auth-state.json");

        LoadState();
    }

    public Task<(bool Success, string? ErrorMessage)> SignUpAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Task.FromResult((false, (string?)"Please enter a valid email address."));
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return Task.FromResult((false, (string?)"Password must be at least 6 characters."));

        CurrentState.IsAuthenticated      = true;
        CurrentState.Email                = email.Trim();
        Touch();
        PersistAndNotify();
        return Task.FromResult((true, (string?)null));
    }

    public Task RefreshSessionAsync() => Task.CompletedTask; // no backend in local mode

    public Task<bool> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(false);
        }

        if (!email.Contains('@') || password.Length < 6)
        {
            return Task.FromResult(false);
        }

        CurrentState.IsAuthenticated = true;
        CurrentState.Email = email.Trim();

        Touch();
        PersistAndNotify();

        return Task.FromResult(true);
    }

    public Task LogoutAsync()
    {
        CurrentState = new AuthState
        {
            IsAuthenticated = false,
            Email = null,
            LastUpdatedUtc = DateTime.UtcNow
        };

        PersistAndNotify();
        return Task.CompletedTask;
    }

    private void LoadState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                CurrentState = new AuthState();
                return;
            }

            var json = File.ReadAllText(_statePath);
            var loaded = JsonSerializer.Deserialize<AuthState>(json);
            CurrentState = loaded ?? new AuthState();
        }
        catch
        {
            CurrentState = new AuthState();
        }
    }

    private void PersistAndNotify()
    {
        try
        {
            var json = JsonSerializer.Serialize(CurrentState, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_statePath, json);
        }
        catch
        {
            // Ignore persistence failures to keep the app usable.
        }

        AuthStateChanged?.Invoke(this, CurrentState);
    }

    private void Touch()
    {
        CurrentState.LastUpdatedUtc = DateTime.UtcNow;
    }
}
