using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauMind.App.Models;
using MauMind.App.Services;
using System.Diagnostics;

namespace MauMind.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IChatService _chatService;
    private readonly IDataService _dataService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly IWebSearchService _webSearchService;
    
    [ObservableProperty]
    private bool _hardwareAccelerationEnabled = true;
    
    [ObservableProperty]
    private int _topK = 5;
    
    [ObservableProperty]
    private int _chunkSize = 512;
    
    [ObservableProperty]
    private int _chunkOverlap = 50;
    
    [ObservableProperty]
    private int _maxTokens = 2048;
    
    [ObservableProperty]
    private double _temperature = 0.7;
    
    [ObservableProperty]
    private string _embeddingModelStatus = "Not loaded";
    
    [ObservableProperty]
    private string _chatModelStatus = "Not loaded";
    
    [ObservableProperty]
    private string _modelsDirectory = string.Empty;
    
    [ObservableProperty]
    private long _memoryUsageMB;
    
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    [ObservableProperty]
    private bool _developerModeEnabled = false;
    
    [ObservableProperty]
    private bool _isDarkMode = false;
    
    // Accessibility properties
    [ObservableProperty]
    private bool _highContrastMode = false;
    
    [ObservableProperty]
    private bool _screenReaderEnabled = false;
    
    [ObservableProperty]
    private string _screenReaderStatus = "Not detected";
    
    [ObservableProperty]
    private bool _webSearchEnabled = true;

    public SettingsViewModel(IEmbeddingService embeddingService, IChatService chatService, IDataService dataService, IAccessibilityService accessibilityService, IWebSearchService webSearchService)
    {
        _embeddingService = embeddingService;
        _chatService = chatService;
        _dataService = dataService;
        _accessibilityService = accessibilityService;
        _webSearchService = webSearchService;

        // Set models directory
        ModelsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind", "models");

        // Load saved theme preference
        LoadThemePreference();

        // Load accessibility settings
        LoadAccessibilitySettings();

        // Load web search preference
        WebSearchEnabled = _webSearchService.IsEnabled;
    }
    
    private void LoadAccessibilitySettings()
    {
        ScreenReaderEnabled = _accessibilityService.IsScreenReaderEnabled;
        ScreenReaderStatus = ScreenReaderEnabled ? "Active" : "Inactive";
        HighContrastMode = _accessibilityService.IsHighContrastMode;
    }
    
    private void LoadThemePreference()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MauMind", "settings.json");
            
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _isDarkMode = settings.IsDarkMode;
                    ApplyTheme();
                }
            }
        }
        catch
        {
            // Use default
        }
    }
    
    partial void OnIsDarkModeChanged(bool value)
    {
        ApplyTheme();
        SaveThemePreference();
    }
    
    private void ApplyTheme()
    {
        Application.Current.UserAppTheme = IsDarkMode 
            ? AppTheme.Dark 
            : AppTheme.Light;
    }
    
    private void SaveThemePreference()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MauMind", "settings.json");
            
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            
            var settings = new AppSettings
            {
                IsDarkMode = IsDarkMode,
                TopK = TopK,
                ChunkSize = ChunkSize,
                ChunkOverlap = ChunkOverlap,
                MaxTokens = MaxTokens,
                Temperature = Temperature,
                HardwareAccelerationEnabled = HardwareAccelerationEnabled
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(settings);
            File.WriteAllText(settingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }
    
    public void Initialize()
    {
        UpdateStatus();
        UpdateMemoryUsage();
    }
    
    private void UpdateStatus()
    {
        EmbeddingModelStatus = _embeddingService.IsModelLoaded ? "Ready" : "Not loaded";
        ChatModelStatus = _chatService.IsModelLoaded ? "Ready" : "Not loaded";
    }
    
    private void UpdateMemoryUsage()
    {
        var process = Process.GetCurrentProcess();
        MemoryUsageMB = process.WorkingSet64 / (1024 * 1024);
    }
    
    [RelayCommand]
    private async Task ReloadModels()
    {
        StatusMessage = "Reloading models...";
        
        try
        {
            _embeddingService.UnloadModel();
            await _embeddingService.LoadModelAsync();
            await _chatService.LoadModelAsync();
            
            UpdateStatus();
            StatusMessage = "Models reloaded successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void ClearCache()
    {
        StatusMessage = "Cache cleared";
        
        // Note: In a real implementation, you'd clear cached data
    }
    
    [RelayCommand]
    private void OpenModelsFolder()
    {
        try
        {
            Directory.CreateDirectory(ModelsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = ModelsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void RefreshMemoryUsage()
    {
        UpdateMemoryUsage();
    }
    
    [RelayCommand]
    private async Task ExportData()
    {
        StatusMessage = "Exporting data...";
        
        try
        {
            var json = await _dataService.ExportDataAsync();
            var fileName = await _dataService.GetExportFileName();
            
            var documentsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MauMind");
            
            Directory.CreateDirectory(documentsPath);
            
            var filePath = Path.Combine(documentsPath, fileName);
            await File.WriteAllTextAsync(filePath, json);
            
            StatusMessage = $"Exported to {fileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private async Task ImportData()
    {
        StatusMessage = "Import feature: Select a JSON file to import";
        
        try
        {
            // Note: In a real app, you'd use FilePicker to select a file
            // For now, we'll just show the status
            StatusMessage = "Tap Import in file picker to continue...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }
    
    partial void OnHighContrastModeChanged(bool value)
    {
        _accessibilityService.SetHighContrastMode(value);
        StatusMessage = value ? "High contrast mode enabled" : "High contrast mode disabled";
    }

    partial void OnWebSearchEnabledChanged(bool value)
    {
        _webSearchService.IsEnabled = value;
        StatusMessage = value ? "Web search enabled" : "Web search disabled";
    }

    [RelayCommand]
    private void RefreshScreenReaderStatus()
    {
        LoadAccessibilitySettings();
    }
}
