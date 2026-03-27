namespace MauMind.App.Models;

/// <summary>
/// Loaded from Resources/Raw/backend-config.json (or the app data copy).
/// Place your real keys there. The template ships with empty strings so
/// the app works offline by default.
/// </summary>
public class BackendConfig
{
    // ──────────────────────────────────────────────────────────────────────────
    // Firebase – get from Firebase Console ▸ Project Settings ▸ Web API Key
    // ──────────────────────────────────────────────────────────────────────────
    public string FirebaseWebApiKey { get; set; } = string.Empty;

    /// <summary>
    /// True when a real Firebase API key is present. False → local-only offline
    /// auth mode is used.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(FirebaseWebApiKey);

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Load from app-data JSON file (non-throwing).</summary>
    public static BackendConfig Load()
    {
        var path = ConfigFilePath();
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<BackendConfig>(json)
                       ?? new BackendConfig();
            }
        }
        catch { /* fall through */ }

        return new BackendConfig();
    }

    public static string ConfigFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind",
            "backend-config.json");
}
