using MauMind.App.Models;

namespace MauMind.App.Services;

/// <summary>
/// Manages downloading, loading, and switching between ONNX language models.
/// </summary>
public class ModelManager : IModelManager, IDisposable
{
    private const string PrefKey = "active_model_id";

    private ModelInfo? _activeModel;
    private bool _isBusy;
    private readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromHours(2)
    };

    public ModelInfo? ActiveModel => _activeModel;
    public bool IsBusy => _isBusy;

    public IReadOnlyList<ModelInfo> AvailableModels =>
        ModelRegistry.All.Select(m =>
        {
            m.IsActive = m.Id == _activeModel?.Id;
            return m;
        }).ToList();

    public event EventHandler<ModelInfo?>? ModelChanged;

    // ─── Initialization ────────────────────────────────────────────────────────

    public async Task InitializeAsync(IProgress<int>? progress = null)
    {
        var savedId = LoadPreference();
        var model   = ModelRegistry.GetById(savedId) ?? ModelRegistry.Default;

        await Task.Run(() =>
        {
            progress?.Report(20);

            if (model.IsDownloaded)
            {
                _activeModel = model;
                progress?.Report(100);
            }
            else
            {
                // Fall back to any downloaded model
                var fallback = ModelRegistry.All.FirstOrDefault(m => m.IsDownloaded);
                _activeModel = fallback;
                progress?.Report(100);
            }
        });

        ModelChanged?.Invoke(this, _activeModel);
    }

    // ─── Switch Model ─────────────────────────────────────────────────────────

    public async Task<bool> SwitchModelAsync(
        string modelId,
        IProgress<double>? downloadProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (_isBusy) return false;

        var model = ModelRegistry.GetById(modelId);
        if (model == null) return false;

        // Already active
        if (_activeModel?.Id == modelId) return true;

        _isBusy = true;
        try
        {
            // Download if needed
            if (!model.IsDownloaded && downloadProgress != null)
            {
                bool downloaded = await DownloadModelAsync(modelId, downloadProgress, cancellationToken);
                if (!downloaded) return false;
            }
            else if (!model.IsDownloaded)
            {
                return false; // No progress reporter and not downloaded
            }

            // Unload current model from memory
            await UnloadCurrentAsync();

            // Set new active
            _activeModel = model;
            SavePreference(modelId);

            ModelChanged?.Invoke(this, _activeModel);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModelManager] Switch failed: {ex.Message}");
            return false;
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ─── Download ─────────────────────────────────────────────────────────────

    public async Task<bool> DownloadModelAsync(
        string modelId,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        var model = ModelRegistry.GetById(modelId);
        if (model == null) return false;
        if (model.IsDownloaded) { progress.Report(1.0); return true; }

        try
        {
            var dir = Path.GetDirectoryName(model.LocalPath)!;
            Directory.CreateDirectory(dir);

            var tmpPath = model.LocalPath + ".tmp";

            using var response = await _http.GetAsync(
                model.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var total   = response.Content.Headers.ContentLength ?? model.FileSizeBytes;
            long received = 0;
            var buf     = new byte[81920];

            await using var src  = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var dest = File.Create(tmpPath);

            int read;
            while ((read = await src.ReadAsync(buf, cancellationToken)) > 0)
            {
                await dest.WriteAsync(buf.AsMemory(0, read), cancellationToken);
                received += read;
                progress.Report((double)received / total);
            }

            File.Move(tmpPath, model.LocalPath, overwrite: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            // Clean up partial file
            var tmp = model.LocalPath + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModelManager] Download failed: {ex.Message}");
            return false;
        }
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    public void DeleteModel(string modelId)
    {
        var model = ModelRegistry.GetById(modelId);
        if (model == null) return;

        // Can't delete the active model
        if (_activeModel?.Id == modelId) return;

        try
        {
            if (File.Exists(model.LocalPath))
                File.Delete(model.LocalPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModelManager] Delete failed: {ex.Message}");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    public string? GetActiveModelPath() => _activeModel?.LocalPath;

    public void SavePreference(string modelId) =>
        Preferences.Set(PrefKey, modelId);

    public string LoadPreference() =>
        Preferences.Get(PrefKey, ModelRegistry.Default.Id);

    private async Task UnloadCurrentAsync()
    {
        // Release managed references; ONNX sessions are disposed in ChatService
        _activeModel = null;

        // Brief pause to let GC collect
        await Task.Delay(300);
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void Dispose() => _http.Dispose();
}
