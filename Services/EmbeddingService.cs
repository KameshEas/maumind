using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Runtime.InteropServices;

namespace MauMind.App.Services;

public class EmbeddingService : IEmbeddingService, IDisposable
{
    private InferenceSession? _session;
    private string _modelPath = string.Empty;
    private bool _isModelLoaded;
    private int _embeddingDimension = 384; // Default for all-MiniLM-L6-v2
    
    public int EmbeddingDimension => _embeddingDimension;
    public bool IsModelLoaded => _isModelLoaded;
    
    public async Task LoadModelAsync(IProgress<int>? progress = null)
    {
        if (_isModelLoaded) return;
        
        await Task.Run(() =>
        {
            try
            {
                progress?.Report(10);
                
                // Get models directory
                var modelsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MauMind", "models");
                
                Directory.CreateDirectory(modelsDir);
                
                _modelPath = Path.Combine(modelsDir, "all-MiniLM-L6-v2.onnx");
                
                // Check if model exists, if not create a dummy for now
                if (!File.Exists(_modelPath))
                {
                    // For now, we'll use a simple embedding approach
                    // In production, you'd download from HuggingFace
                    _embeddingDimension = 384;
                }
                
                progress?.Report(50);
                
                // Try to load ONNX model if it exists
                if (File.Exists(_modelPath))
                {
                    var sessionOptions = new SessionOptions();
                    sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    
                    _session = new InferenceSession(_modelPath, sessionOptions);
                }
                
                progress?.Report(100);
                _isModelLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading embedding model: {ex.Message}");
                // Continue with fallback implementation
                _isModelLoaded = true;
            }
        });
    }
    
    public async Task<float[]> GenerateEmbeddingAsync(string text, IProgress<int>? progress = null)
    {
        if (!_isModelLoaded)
        {
            await LoadModelAsync(progress);
        }
        
        return await Task.Run(() =>
        {
            // If ONNX model is loaded, use it
            if (_session != null)
            {
                try
                {
                    return GenerateEmbeddingWithOnnx(text);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ONNX inference error: {ex.Message}");
                }
            }
            
            // Fallback: Simple hash-based embedding for demonstration
            // In production, this would be replaced with actual model inference
            return GenerateFallbackEmbedding(text);
        });
    }
    
    private float[] GenerateEmbeddingWithOnnx(string text)
    {
        // Tokenize and create input tensor
        var tokens = SimpleTokenize(text);
        var longTokens = tokens.Select(t => (long)t).ToArray();
        
        var inputIds = new DenseTensor<long>(new[] { 1, longTokens.Length });
        for (int i = 0; i < longTokens.Length; i++)
        {
            inputIds[0, i] = longTokens[i];
        }
        
        var attentionMask = new DenseTensor<long>(new[] { 1, Math.Max(longTokens.Length, 1) });
        for (int i = 0; i < longTokens.Length; i++)
        {
            attentionMask[0, i] = 1;
        }
        
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };
        
        using var results = _session.Run(inputs);
        var output = results.FirstOrDefault();
        
        if (output != null)
        {
            return MeanPooling(output.AsTensor<float>(), attentionMask);
        }
        
        return GenerateFallbackEmbedding(text);
    }
    
    private List<int> SimpleTokenize(string text)
    {
        // Simple word-based tokenization for demonstration
        // In production, use proper tokenizer (HuggingFace.Tokenizers)
        var words = text.ToLower().Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?' }, 
            StringSplitOptions.RemoveEmptyEntries);
        
        // Convert to token IDs (simplified - maps to hash for consistency)
        var tokens = new List<int>();
        foreach (var word in words)
        {
            tokens.Add(Math.Abs(word.GetHashCode()) % 50000);
        }
        
        // Add special tokens: [CLS] = 101, [SEP] = 102, [PAD] = 0
        tokens.Insert(0, 101); // [CLS]
        tokens.Add(102); // [SEP]
        
        // Pad to minimum length
        while (tokens.Count < 4)
        {
            tokens.Add(0);
        }
        
        return tokens;
    }
    
    private float[] MeanPooling(Tensor<float> embeddings, Tensor<long> attentionMask)
    {
        var batchSize = embeddings.Dimensions[0];
        var seqLength = embeddings.Dimensions[1];
        var hiddenSize = embeddings.Dimensions[2];
        
        var result = new float[hiddenSize];
        
        for (int b = 0; b < batchSize; b++)
        {
            float sum = 0;
            for (int i = 0; i < seqLength; i++)
            {
                if (attentionMask[b, i] == 1)
                {
                    for (int h = 0; h < hiddenSize; h++)
                    {
                        result[h] += embeddings[b, i, h];
                    }
                    sum++;
                }
            }
            
            if (sum > 0)
            {
                for (int h = 0; h < hiddenSize; h++)
                {
                    result[h] /= sum;
                }
            }
        }
        
        // Normalize
        float magnitude = 0;
        foreach (var v in result)
        {
            magnitude += v * v;
        }
        magnitude = MathF.Sqrt(magnitude);
        
        if (magnitude > 0)
        {
            for (int i = 0; i < result.Length; i++)
            {
                result[i] /= magnitude;
            }
        }
        
        return result;
    }
    
    private float[] GenerateFallbackEmbedding(string text)
    {
        // Simple hash-based embedding for demonstration
        // Produces consistent embeddings based on text content
        var embedding = new float[_embeddingDimension];
        
        // Use text hash to seed random for consistency
        var hash = text.GetHashCode();
        var random = new Random(hash);
        
        // Create a sparse embedding
        for (int i = 0; i < _embeddingDimension; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }
        
        // Normalize
        float magnitude = 0;
        foreach (var v in embedding)
        {
            magnitude += v * v;
        }
        magnitude = MathF.Sqrt(magnitude);
        
        if (magnitude > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }
        }
        
        return embedding;
    }
    
    public void UnloadModel()
    {
        _session?.Dispose();
        _session = null;
        _isModelLoaded = false;
    }
    
    public void Dispose()
    {
        UnloadModel();
    }
}
