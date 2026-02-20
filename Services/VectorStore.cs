using MauMind.App.Data;
using MauMind.App.Models;

namespace MauMind.App.Services;

public class VectorStore : IVectorStore
{
    private readonly DatabaseService _databaseService;
    private readonly IEmbeddingService _embeddingService;
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    
    public VectorStore(DatabaseService databaseService, IEmbeddingService embeddingService)
    {
        _databaseService = databaseService;
        _embeddingService = embeddingService;
        // Reduced from 512 to 256 for more focused context
        _chunkSize = 256;
        // Reduced overlap from 50 to 25
        _chunkOverlap = 25;
    }
    
    public async Task InitializeAsync()
    {
        await _embeddingService.LoadModelAsync();
    }
    
    public async Task AddDocumentVectorsAsync(int documentId, string content)
    {
        // Delete existing vectors for this document
        await _databaseService.DeleteVectorEntriesByDocumentIdAsync(documentId);
        
        // Chunk the content
        var chunks = ChunkText(content);
        
        // Generate embeddings for each chunk
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
            
            // Convert float[] to byte[]
            var embeddingBytes = FloatToBytes(embedding);
            
            var vectorEntry = new VectorEntry
            {
                DocumentId = documentId,
                ChunkText = chunk,
                ChunkIndex = i,
                Embedding = embeddingBytes
            };
            
            await _databaseService.InsertVectorEntryAsync(vectorEntry);
        }
    }
    
    public async Task<List<(VectorEntry Entry, float Score)>> SearchAsync(string query, int topK = 5)
    {
        // Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        
        // Get all vector entries
        var entries = await _databaseService.GetAllVectorEntriesAsync();
        
        // Calculate cosine similarity for each entry
        var scoredResults = new List<(VectorEntry Entry, float Score)>();
        
        foreach (var entry in entries)
        {
            var entryEmbedding = BytesToFloat(entry.Embedding);
            var similarity = CosineSimilarity(queryEmbedding, entryEmbedding);
            scoredResults.Add((entry, similarity));
        }
        
        // Sort by similarity and take top K
        var results = scoredResults
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
        
        return results;
    }
    
    public async Task DeleteDocumentVectorsAsync(int documentId)
    {
        await _databaseService.DeleteVectorEntriesByDocumentIdAsync(documentId);
    }
    
    private List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }
        
        // Simple chunking by sentences/paragraphs
        var sentences = text.Split(new[] { '.', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        var currentChunk = new System.Text.StringBuilder();
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            if (currentChunk.Length + trimmed.Length > _chunkSize)
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    
                    // Handle overlap
                    var overlapStart = Math.Max(0, currentChunk.Length - _chunkOverlap);
                    var overlapText = currentChunk.ToString().Substring(overlapStart);
                    currentChunk.Clear();
                    currentChunk.Append(overlapText);
                }
            }
            
            currentChunk.Append(trimmed);
            currentChunk.Append(". ");
        }
        
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }
        
        return chunks;
    }
    
    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same dimension");
        }
        
        float dotProduct = 0;
        float normA = 0;
        float normB = 0;
        
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        
        if (denominator == 0)
        {
            return 0;
        }
        
        return dotProduct / denominator;
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
}
