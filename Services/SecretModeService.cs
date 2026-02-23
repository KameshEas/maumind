using System.Security.Cryptography;
using System.Text;

namespace MauMind.App.Services;

public interface ISecretModeService
{
    bool IsSecretModeEnabled { get; }
    bool IsPinSet { get; }
    Task SetPinAsync(string pin);
    Task<bool> ValidatePinAsync(string pin);
    Task<bool> RemovePinAsync(string currentPin);
    void EnableSecretMode();
    void DisableSecretMode();
    string HashPin(string pin);
}

public class SecretModeService : ISecretModeService
{
    private const string SETTINGS_FILE = "secret_mode.json";
    private string? _storedPinHash;
    private bool _isSecretModeEnabled;

    public bool IsSecretModeEnabled => _isSecretModeEnabled;
    public bool IsPinSet => !string.IsNullOrEmpty(_storedPinHash);

    public SecretModeService()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            var settingsPath = GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<SecretModeSettings>(json);
                if (settings != null)
                {
                    _storedPinHash = settings.PinHash;
                    _isSecretModeEnabled = settings.IsEnabled;
                }
            }
        }
        catch
        {
            // Use defaults
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settingsPath = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            
            var settings = new SecretModeSettings
            {
                PinHash = _storedPinHash,
                IsEnabled = _isSecretModeEnabled
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(settings);
            File.WriteAllText(settingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind", SETTINGS_FILE);
    }

    public Task SetPinAsync(string pin)
    {
        _storedPinHash = HashPin(pin);
        SaveSettings();
        return Task.CompletedTask;
    }

    public Task<bool> ValidatePinAsync(string pin)
    {
        if (string.IsNullOrEmpty(_storedPinHash))
            return Task.FromResult(false);

        var inputHash = HashPin(pin);
        var isValid = inputHash == _storedPinHash;
        
        // If PIN is valid and secret mode is enabled, activate it
        if (isValid && _isSecretModeEnabled)
        {
            // Secret mode already enabled, just validate
        }
        
        return Task.FromResult(isValid);
    }

    public Task<bool> RemovePinAsync(string currentPin)
    {
        if (ValidatePinAsync(currentPin).Result)
        {
            _storedPinHash = null;
            _isSecretModeEnabled = false;
            SaveSettings();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public void EnableSecretMode()
    {
        _isSecretModeEnabled = true;
        SaveSettings();
    }

    public void DisableSecretMode()
    {
        _isSecretModeEnabled = false;
        SaveSettings();
    }

    public string HashPin(string pin)
    {
        // Use SHA256 with a salt for secure hashing
        var salt = "MauMind_SecretMode_v1";
        var combined = pin + salt;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(bytes);
    }

    private class SecretModeSettings
    {
        public string? PinHash { get; set; }
        public bool IsEnabled { get; set; }
    }
}
