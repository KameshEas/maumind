namespace MauMind.App.Models;

public enum ModelCapability
{
    GeneralQA      = 1 << 0,
    Reasoning      = 1 << 1,
    Summarization  = 1 << 2,
    Creative       = 1 << 3,
    CodeAssist     = 1 << 4,
    Multilingual   = 1 << 5,
}

public class ModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public int MinRamMB { get; set; }
    public string Badge { get; set; } = string.Empty;
    public string Icon { get; set; } = "🤖";
    public string BadgeColor { get; set; } = "#0078D4";
    public ModelCapability Capabilities { get; set; }

    // Computed properties
    public bool IsDownloaded => File.Exists(LocalPath);
    
    public string LocalPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MauMind", "models", FileName);

    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1_000_000_000 => $"{FileSizeBytes / 1_000_000.0:0.0} MB",
        _ => $"{FileSizeBytes / 1_000_000_000.0:0.0} GB"
    };

    public bool IsActive { get; set; }

    public bool HasCapability(ModelCapability cap) => (Capabilities & cap) != 0;
}
