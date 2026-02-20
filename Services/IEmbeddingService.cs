namespace MauMind.App.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, IProgress<int>? progress = null);
    int EmbeddingDimension { get; }
    bool IsModelLoaded { get; }
    Task LoadModelAsync(IProgress<int>? progress = null);
    void UnloadModel();
}
