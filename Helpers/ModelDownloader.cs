using System.Net.Http;

namespace MauMind.App.Helpers;

public class ModelDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _modelsDirectory;
    
    // Hugging Face model URLs
    private const string EmbeddingModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";
    private const string EmbeddingModelName = "all-MiniLM-L6-v2.onnx";
    
    // For Phi-3 Mini, we'll use a smaller quantized version
    private const string LanguageModelUrl = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnnx/resolve/main/phi-3-mini-4k-instruct-q4.onnx";
    private const string LanguageModelName = "phi-3-mini-4k-instruct-q4.onnx";
    
    public ModelDownloader()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        
        _modelsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MauMind", "models");
        
        Directory.CreateDirectory(_modelsDirectory);
    }
    
    public async Task DownloadModelsAsync(IProgress<int>? progress = null)
    {
        progress?.Report(10);
        
        // Download embedding model
        var embeddingPath = Path.Combine(_modelsDirectory, EmbeddingModelName);
        if (!File.Exists(embeddingPath))
        {
            await DownloadFileAsync(EmbeddingModelUrl, embeddingPath, "Downloading embedding model...");
        }
        
        progress?.Report(50);
        
        // Download language model
        var languagePath = Path.Combine(_modelsDirectory, LanguageModelName);
        if (!File.Exists(languagePath))
        {
            await DownloadFileAsync(LanguageModelUrl, languagePath, "Downloading language model...");
        }
        
        progress?.Report(100);
    }
    
    private async Task DownloadFileAsync(string url, string destinationPath, string statusMessage)
    {
        try
        {
            Console.WriteLine(statusMessage);
            
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var downloadedBytes = 0L;
            
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            var buffer = new byte[8192];
            int bytesRead;
            
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;
                
                if (totalBytes > 0)
                {
                    var progressPercent = (int)((downloadedBytes * 100) / totalBytes);
                    Console.WriteLine($"Downloaded {progressPercent}% ({downloadedBytes / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB)");
                }
            }
            
            Console.WriteLine($"Downloaded to: {destinationPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading {url}: {ex.Message}");
            throw;
        }
    }
    
    public string GetEmbeddingModelPath() => Path.Combine(_modelsDirectory, EmbeddingModelName);
    public string GetLanguageModelPath() => Path.Combine(_modelsDirectory, LanguageModelName);
    
    public bool AreModelsReady()
    {
        var embeddingPath = Path.Combine(_modelsDirectory, EmbeddingModelName);
        var languagePath = Path.Combine(_modelsDirectory, LanguageModelName);
        
        return File.Exists(embeddingPath) && File.Exists(languagePath);
    }
}
