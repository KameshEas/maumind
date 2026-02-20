namespace MauMind.App.Models;

/// <summary>
/// Static catalog of all supported ONNX language models.
/// </summary>
public static class ModelRegistry
{
    public static readonly IReadOnlyList<ModelInfo> All = new List<ModelInfo>
    {
        new ModelInfo
        {
            Id           = "phi3-mini",
            DisplayName  = "Phi-3 Mini",
            Description  = "Microsoft's compact powerhouse. Great for reasoning, Q&A and summarization.",
            Version      = "3.8B Q4",
            FileSizeBytes = 2_300_000_000L,
            FileName     = "phi3-mini-4k-instruct-q4.onnx",
            DownloadUrl  = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/resolve/main/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/phi3-mini-4k-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx",
            MinRamMB     = 3000,
            Badge        = "Recommended",
            BadgeColor   = "#0078D4",
            Icon         = "🏆",
            Capabilities = ModelCapability.GeneralQA | ModelCapability.Reasoning | ModelCapability.Summarization,
        },
        new ModelInfo
        {
            Id           = "gemma-2b",
            DisplayName  = "Gemma 2B",
            Description  = "Google's efficient model. Excellent for creative and concise responses.",
            Version      = "2B Q4",
            FileSizeBytes = 1_500_000_000L,
            FileName     = "gemma-2b-it-q4.onnx",
            DownloadUrl  = "https://huggingface.co/google/gemma-2b-it-onnx/resolve/main/gemma-2b-it-q4.onnx",
            MinRamMB     = 2000,
            Badge        = "Creative",
            BadgeColor   = "#5B5FC7",
            Icon         = "💡",
            Capabilities = ModelCapability.GeneralQA | ModelCapability.Creative | ModelCapability.Summarization,
        },
        new ModelInfo
        {
            Id           = "llama-3-1b",
            DisplayName  = "Llama 3.2 1B",
            Description  = "Meta's lightweight model. Fast responses with good general knowledge.",
            Version      = "1B Q4",
            FileSizeBytes = 800_000_000L,
            FileName     = "llama-3.2-1b-instruct-q4.onnx",
            DownloadUrl  = "https://huggingface.co/meta-llama/Llama-3.2-1B-Instruct-ONNX/resolve/main/llama-3.2-1b-instruct-q4.onnx",
            MinRamMB     = 1500,
            Badge        = "Fast",
            BadgeColor   = "#00897B",
            Icon         = "⚡",
            Capabilities = ModelCapability.GeneralQA | ModelCapability.Summarization,
        },
        new ModelInfo
        {
            Id           = "tinyllama",
            DisplayName  = "TinyLlama 1.1B",
            Description  = "Ultra-lightweight. Perfect for low-RAM devices. Fastest inference.",
            Version      = "1.1B Q4",
            FileSizeBytes = 400_000_000L,
            FileName     = "tinyllama-1.1b-chat-q4.onnx",
            DownloadUrl  = "https://huggingface.co/TinyLlama/TinyLlama-1.1B-Chat-v1.0-ONNX/resolve/main/tinyllama-1.1b-chat-q4.onnx",
            MinRamMB     = 800,
            Badge        = "Lightweight",
            BadgeColor   = "#FF8C00",
            Icon         = "🚀",
            Capabilities = ModelCapability.GeneralQA,
        },
    };

    public static ModelInfo? GetById(string id) =>
        All.FirstOrDefault(m => m.Id == id);

    public static ModelInfo Default => All[0]; // Phi-3 Mini
}
