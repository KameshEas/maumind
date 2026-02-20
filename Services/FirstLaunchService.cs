using System.Text.Json;

namespace MauMind.App.Services;

public interface IFirstLaunchService
{
    bool IsFirstLaunch { get; }
    bool IsOnboardingComplete { get; set; }
    void CompleteOnboarding();
    void ShowFeatureHint(string featureKey);
    bool ShouldShowFeatureHint(string featureKey);
}

public class FirstLaunchService : IFirstLaunchService
{
    private readonly string _settingsPath;
    private OnboardingSettings? _settings;
    
    public bool IsFirstLaunch => _settings == null || _settings.IsFirstLaunch;
    public bool IsOnboardingComplete 
    { 
        get => _settings?.OnboardingComplete ?? false;
        set 
        {
            if (_settings != null)
            {
                _settings.OnboardingComplete = value;
                SaveSettings();
            }
        }
    }
    
    public FirstLaunchService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind");
        
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "onboarding.json");
        
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<OnboardingSettings>(json);
            }
            
            if (_settings == null)
            {
                _settings = new OnboardingSettings { IsFirstLaunch = true };
            }
        }
        catch
        {
            _settings = new OnboardingSettings { IsFirstLaunch = true };
        }
    }
    
    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
    
    public void CompleteOnboarding()
    {
        if (_settings != null)
        {
            _settings.IsFirstLaunch = false;
            _settings.OnboardingComplete = true;
            SaveSettings();
        }
    }
    
    public void ShowFeatureHint(string featureKey)
    {
        if (_settings != null && _settings.ShownHints == null)
        {
            _settings.ShownHints = new List<string>();
        }
        
        if (_settings != null && !_settings.ShownHints.Contains(featureKey))
        {
            _settings.ShownHints.Add(featureKey);
            SaveSettings();
        }
    }
    
    public bool ShouldShowFeatureHint(string featureKey)
    {
        return _settings?.ShownHints?.Contains(featureKey) != true;
    }
}

public class OnboardingSettings
{
    public bool IsFirstLaunch { get; set; } = true;
    public bool OnboardingComplete { get; set; } = false;
    public List<string>? ShownHints { get; set; }
}
