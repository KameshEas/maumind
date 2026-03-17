using MauMind.App.Data;
using MauMind.App.Models;

namespace MauMind.App.Services;

public class MemoryService : IMemoryService
{
    private readonly DatabaseService _databaseService;
    private readonly IEmbeddingService _embeddingService;
    private readonly int _chunkSize = 256;
    private readonly int _chunkOverlap = 25;

    public MemoryService(DatabaseService databaseService, IEmbeddingService embeddingService)
    {
        _databaseService = databaseService;
        _embeddingService = embeddingService;
    }

    public async Task<int> AddMemoryAsync(string title, string content, bool pinned = false)
    {
        var memory = new MemoryEntry
        {
            Title = title,
            Content = content,
            Pinned = pinned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var memoryId = await _databaseService.InsertMemoryAsync(memory);

        // Chunk and embed
        var chunks = ChunkText(content);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
            var embeddingBytes = FloatToBytes(embedding);

            var vec = new MemoryVectorEntry
            {
                MemoryId = memoryId,
                ChunkText = chunk,
                ChunkIndex = i,
                Embedding = embeddingBytes
            };

            await _databaseService.InsertMemoryVectorAsync(vec);
        }

        return memoryId;
    }

    public async Task<List<(MemoryEntry Memory, float Score)>> RetrieveAsync(string query, int topK = 5)
    {
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        var entries = await _databaseService.GetAllMemoryVectorsAsync();
        var scored = new List<(MemoryEntry, float)>();

        foreach (var entry in entries)
        {
            var entryEmbedding = BytesToFloat(entry.Embedding);
            var score = CosineSimilarity(queryEmbedding, entryEmbedding);
            var memory = await _databaseService.GetMemoryByIdAsync(entry.MemoryId);
            if (memory != null)
            {
                scored.Add((memory, score));
            }
        }

        var results = scored
            .GroupBy(x => x.Item1.Id)
            .Select(g => (Memory: g.First().Item1, Score: g.Max(x => x.Item2)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        return results;
    }

    public Task<List<MemoryEntry>> GetAllMemoriesAsync()
    {
        return _databaseService.GetAllMemoriesAsync();
    }

    public Task<MemoryEntry?> GetMemoryByIdAsync(int id)
    {
        return _databaseService.GetMemoryByIdAsync(id);
    }

    public async Task DeleteMemoryAsync(int id)
    {
        await _databaseService.DeleteMemoryVectorsByMemoryIdAsync(id);
        await _databaseService.DeleteMemoryAsync(id);
    }

    private List<string> ChunkText(string text)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var sentences = text.Split(new[] { '.', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var current = new System.Text.StringBuilder();

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (current.Length + trimmed.Length > _chunkSize)
            {
                if (current.Length > 0)
                {
                    chunks.Add(current.ToString().Trim());
                    var overlapStart = Math.Max(0, current.Length - _chunkOverlap);
                    var overlapText = current.ToString().Substring(overlapStart);
                    current.Clear();
                    current.Append(overlapText);
                }
            }

            current.Append(trimmed);
            current.Append(". ");
        }

        if (current.Length > 0) chunks.Add(current.ToString().Trim());

        return chunks;
    }

    private byte[] FloatToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private float[] BytesToFloat(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        var denom = MathF.Sqrt(na) * MathF.Sqrt(nb);
        if (denom == 0) return 0;
        return dot / denom;
    }
}
