namespace MauMind.App.Models;

public class AppSettings
{
    public string EmbeddingModelPath { get; set; } = "models/all-MiniLM-L6-v2.onnx";
    public string LanguageModelPath { get; set; } = "models/phi-3-mini-4k-instruct-q4.onnx";
    public int TopK { get; set; } = 5;
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public bool HardwareAccelerationEnabled { get; set; } = true;
    public bool IsDarkMode { get; set; } = false;
}
