using System.Text.Json;
using MauMind.App.Models;

namespace MauMind.App.Services;

/// <summary>
/// Production <see cref="IAuthService"/> implementation that delegates to:
///   • Firebase Auth REST API  – for identity (sign-in / sign-up / token refresh)
///
/// When <see cref="BackendConfig.IsConfigured"/> is false (keys not set) it falls back
/// to the same local-only behaviour as the original <see cref="AuthService"/> so the
/// app continues to work in development / offline scenarios.
/// </summary>
public sealed class RemoteAuthService : IAuthService
{
    // SecureStorage key names
    private const string KeyIdToken      = "firebase_id_token";
    private const string KeyRefreshToken = "firebase_refresh_token";
    private const string KeyUserId       = "firebase_user_id";
    private const string KeyTokenExpiry  = "firebase_token_expiry";

    private readonly IFirebaseAuthClient _firebase;
    private readonly BackendConfig       _config;
    private readonly string              _statePath;
    private readonly SemaphoreSlim       _refreshLock = new(1, 1);

    public AuthState CurrentState { get; private set; } = new();

    public event EventHandler<AuthState>? AuthStateChanged;

    public RemoteAuthService(
        IFirebaseAuthClient firebase,
        BackendConfig       config)
    {
        _firebase   = firebase;
        _config     = config;

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind");
        Directory.CreateDirectory(dataDir);
        _statePath = Path.Combine(dataDir, "auth-state.json");

        LoadPersistedState();

        // Try a background refresh so cached token state is up-to-date at startup.
        _ = Task.Run(RefreshSessionAsync);
    }

    // ─── Sign in ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return false;

        if (!_config.IsConfigured)
            return LocalFallbackLogin(email, password);

        try
        {
            var result = await _firebase.SignInAsync(email, password).ConfigureAwait(false);
            await PersistTokensAsync(result).ConfigureAwait(false);

            CurrentState = new AuthState
            {
                IsAuthenticated     = true,
                Email               = result.Email,
                LastUpdatedUtc      = DateTime.UtcNow
            };

            PersistAndNotify();
            return true;
        }
        catch (FirebaseAuthException)
        {
            throw;          // let ViewModel surface the user-friendly message
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RemoteAuth] Login error: {ex.Message}");
            return false;   // network / unexpected failure
        }
    }

    // ─── Sign up ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(bool Success, string? ErrorMessage)> SignUpAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (false, "Please enter a valid email address.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "Password must be at least 6 characters.");

        if (!_config.IsConfigured)
        {
            var ok = LocalFallbackLogin(email, password);
            return ok ? (true, null) : (false, "Sign-up failed.");
        }

        try
        {
            var result = await _firebase.SignUpAsync(email, password).ConfigureAwait(false);
            await PersistTokensAsync(result).ConfigureAwait(false);

            CurrentState = new AuthState
            {
                IsAuthenticated      = true,
                Email                = result.Email,
                LastUpdatedUtc       = DateTime.UtcNow
            };

            PersistAndNotify();
            return (true, null);
        }
        catch (FirebaseAuthException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception)
        {
            return (false, "Unable to create account. Please check your connection and try again.");
        }
    }

    // ─── Sign out ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task LogoutAsync()
    {
        await ClearStoredTokensAsync().ConfigureAwait(false);

        CurrentState = new AuthState
        {
            IsAuthenticated      = false,
            Email                = null,
            LastUpdatedUtc       = DateTime.UtcNow
        };

        PersistAndNotify();
    }

    // ─── Token refresh ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task RefreshSessionAsync()
    {
        if (!CurrentState.IsAuthenticated || !_config.IsConfigured)
            return;

        // Prevent concurrent refreshes
        if (!await _refreshLock.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            var expiryStr = await TryGetSecureAsync(KeyTokenExpiry).ConfigureAwait(false);
            if (DateTime.TryParse(expiryStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry)
                && expiry > DateTime.UtcNow.AddMinutes(5))
            {
                // Token still valid — skip refresh.
                if ((DateTime.UtcNow - CurrentState.LastUpdatedUtc).TotalMinutes < 30)
                    return;
            }

            var refreshToken = await TryGetSecureAsync(KeyRefreshToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(refreshToken))
                return;

            var refreshed = await _firebase.RefreshTokenAsync(refreshToken).ConfigureAwait(false);
            if (refreshed is null)
                return;

            var expiresIn = int.TryParse(refreshed.ExpiresIn, out var s) ? s : 3600;
            await TrySetSecureAsync(KeyIdToken,     refreshed.IdToken).ConfigureAwait(false);
            await TrySetSecureAsync(KeyRefreshToken, refreshed.RefreshToken).ConfigureAwait(false);
            await TrySetSecureAsync(KeyTokenExpiry,
                DateTime.UtcNow.AddSeconds(expiresIn).ToString("O")).ConfigureAwait(false);

            CurrentState.LastUpdatedUtc = DateTime.UtcNow;
            PersistAndNotify();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RemoteAuth] Silent refresh failed: {ex.Message}");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private bool LocalFallbackLogin(string email, string password)
    {
        if (!email.Contains('@') || password.Length < 6)
            return false;

        CurrentState.IsAuthenticated = true;
        CurrentState.Email           = email.Trim();
        CurrentState.LastUpdatedUtc = DateTime.UtcNow;

        PersistAndNotify();
        return true;
    }

    private async Task PersistTokensAsync(FirebaseSignInResult r)
    {
        var expiresIn = int.TryParse(r.ExpiresIn, out var s) ? s : 3600;

        await TrySetSecureAsync(KeyIdToken,      r.IdToken).ConfigureAwait(false);
        await TrySetSecureAsync(KeyRefreshToken,  r.RefreshToken).ConfigureAwait(false);
        await TrySetSecureAsync(KeyUserId,        r.LocalId).ConfigureAwait(false);
        await TrySetSecureAsync(KeyTokenExpiry,
            DateTime.UtcNow.AddSeconds(expiresIn).ToString("O")).ConfigureAwait(false);
    }

    private static async Task ClearStoredTokensAsync()
    {
        await TryRemoveSecureAsync(KeyIdToken);
        await TryRemoveSecureAsync(KeyRefreshToken);
        await TryRemoveSecureAsync(KeyUserId);
        await TryRemoveSecureAsync(KeyTokenExpiry);
    }

    private void LoadPersistedState()
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
        catch { }

        AuthStateChanged?.Invoke(this, CurrentState);
    }

    // SecureStorage wrappers — never throw so the service remains usable on
    // platforms / test runners where SecureStorage is not available.
    private static async Task TrySetSecureAsync(string key, string value)
    {
        try { await SecureStorage.SetAsync(key, value).ConfigureAwait(false); } catch { }
    }

    private static async Task<string?> TryGetSecureAsync(string key)
    {
        try { return await SecureStorage.GetAsync(key).ConfigureAwait(false); } catch { return null; }
    }

    private static Task TryRemoveSecureAsync(string key)
    {
        try { SecureStorage.Remove(key); } catch { }
        return Task.CompletedTask;
    }
}
