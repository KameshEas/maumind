using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauMind.App.Services;

// ─── Public contract ──────────────────────────────────────────────────────────

public interface IFirebaseAuthClient
{
    /// <summary>Sign in an existing account. Throws <see cref="FirebaseAuthException"/> on bad credentials.</summary>
    Task<FirebaseSignInResult> SignInAsync(string email, string password);

    /// <summary>Create a new account. Throws <see cref="FirebaseAuthException"/> on error (duplicate email, etc.).</summary>
    Task<FirebaseSignInResult> SignUpAsync(string email, string password);

    /// <summary>Exchange a refresh token for a fresh ID token.  Returns null when offline or token is revoked.</summary>
    Task<FirebaseRefreshResult?> RefreshTokenAsync(string refreshToken);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public sealed class FirebaseAuthClient : IFirebaseAuthClient
{
    private const string AuthBase  = "https://identitytoolkit.googleapis.com/v1/accounts";
    private const string TokenBase = "https://securetoken.googleapis.com/v1/token";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string     _apiKey;

    public FirebaseAuthClient(HttpClient http, string apiKey)
    {
        _http   = http;
        _apiKey = apiKey;
    }

    public Task<FirebaseSignInResult> SignInAsync(string email, string password) =>
        PostAuthAsync("signInWithPassword", email, password);

    public Task<FirebaseSignInResult> SignUpAsync(string email, string password) =>
        PostAuthAsync("signUp", email, password);

    public async Task<FirebaseRefreshResult?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var url  = $"{TokenBase}?key={Uri.EscapeDataString(_apiKey)}";
            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type",     "refresh_token"),
                new KeyValuePair<string, string>("refresh_token",  refreshToken)
            });

            using var response = await _http.PostAsync(url, body).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<FirebaseRefreshResult>(json, JsonOpts);
        }
        catch (HttpRequestException)
        {
            return null; // offline
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<FirebaseSignInResult> PostAuthAsync(string action, string email, string password)
    {
        var url  = $"{AuthBase}:{action}?key={Uri.EscapeDataString(_apiKey)}";
        var body = new { email, password, returnSecureToken = true };

        using var response = await _http.PostAsJsonAsync(url, body).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var err = JsonSerializer.Deserialize<FirebaseErrorEnvelope>(json, JsonOpts);
            throw new FirebaseAuthException(MapErrorCode(err?.Error?.Message));
        }

        return JsonSerializer.Deserialize<FirebaseSignInResult>(json, JsonOpts)
               ?? throw new FirebaseAuthException("Unexpected empty response from Firebase.");
    }

    private static string MapErrorCode(string? code) => code switch
    {
        "EMAIL_NOT_FOUND"                                          => "No account found for this email address.",
        "INVALID_PASSWORD"                                         => "Incorrect password.",
        "INVALID_LOGIN_CREDENTIALS"                                => "Incorrect email or password.",
        "USER_DISABLED"                                            => "This account has been disabled.",
        "EMAIL_EXISTS"                                             => "An account already exists with this email.",
        "OPERATION_NOT_ALLOWED"                                    => "Email/password sign-in is not enabled.",
        "TOO_MANY_ATTEMPTS_TRY_LATER"                              => "Too many failed attempts. Please try again later.",
        "WEAK_PASSWORD : Password should be at least 6 characters" => "Password must be at least 6 characters.",
        _ => code ?? "Authentication failed. Please try again."
    };
}

// ─── Exception ────────────────────────────────────────────────────────────────

public sealed class FirebaseAuthException : Exception
{
    public FirebaseAuthException(string message) : base(message) { }
}

// ─── REST response models ─────────────────────────────────────────────────────

public sealed class FirebaseSignInResult
{
    [JsonPropertyName("idToken")]      public string IdToken      { get; set; } = string.Empty;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("localId")]      public string LocalId      { get; set; } = string.Empty;
    [JsonPropertyName("email")]        public string Email        { get; set; } = string.Empty;
    [JsonPropertyName("expiresIn")]    public string ExpiresIn    { get; set; } = "3600";
}

public sealed class FirebaseRefreshResult
{
    [JsonPropertyName("id_token")]      public string IdToken      { get; set; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("user_id")]       public string UserId       { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")]    public string ExpiresIn    { get; set; } = "3600";
}

internal sealed class FirebaseErrorEnvelope
{
    [JsonPropertyName("error")] public FirebaseErrorDetail? Error { get; set; }
}

internal sealed class FirebaseErrorDetail
{
    [JsonPropertyName("message")] public string? Message { get; set; }
}
