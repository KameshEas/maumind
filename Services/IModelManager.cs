using MauMind.App.Models;

namespace MauMind.App.Services;

public interface IModelManager
{
    /// <summary>Currently active model (null if none loaded).</summary>
    ModelInfo? ActiveModel { get; }

    /// <summary>All models in the registry with live IsDownloaded / IsActive state.</summary>
    IReadOnlyList<ModelInfo> AvailableModels { get; }

    /// <summary>True while a model load/switch is in progress.</summary>
    bool IsBusy { get; }

    /// <summary>Fired whenever the active model changes.</summary>
    event EventHandler<ModelInfo?> ModelChanged;

    /// <summary>Load the saved (or default) model on startup.</summary>
    Task InitializeAsync(IProgress<int>? progress = null);

    /// <summary>Switch to a different model. Downloads it first if needed.</summary>
    Task<bool> SwitchModelAsync(string modelId,
        IProgress<double>? downloadProgress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Download a model file without switching to it.</summary>
    Task<bool> DownloadModelAsync(string modelId,
        IProgress<double> progress,
        CancellationToken cancellationToken = default);

    /// <summary>Delete downloaded model file to free space.</summary>
    void DeleteModel(string modelId);

    /// <summary>Returns the local ONNX model path for use in inference.</summary>
    string? GetActiveModelPath();

    /// <summary>Persist selected model id to preferences.</summary>
    void SavePreference(string modelId);

    /// <summary>Load persisted model preference.</summary>
    string LoadPreference();
}
