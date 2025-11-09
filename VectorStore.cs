using System.Text.Json;

namespace GER;

public class VectorStore
{
    private readonly List<Chunk> _chunks = [];
    private readonly string _storagePath;

    public VectorStore(string storagePath)
    {
        _storagePath = storagePath;
        LoadFromDisk();
    }

    public void AddChunk(Chunk chunk)
    {
        if (chunk.Embedding == null)
        {
            throw new ArgumentException("Chunk must have an embedding", nameof(chunk));
        }

        // Remove existing chunk with same ID if it exists
        _chunks.RemoveAll(c => c.Id == chunk.Id);
        _chunks.Add(chunk);
    }

    public void AddChunks(IEnumerable<Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            AddChunk(chunk);
        }
    }

    public List<SearchResult> Search(float[] queryEmbedding, int topK = 5)
    {
        var results = new List<SearchResult>();

        foreach (var chunk in _chunks)
        {
            if (chunk.Embedding == null)
                continue;

            var similarity = CosineSimilarity(queryEmbedding, chunk.Embedding);
            results.Add(new SearchResult { Chunk = chunk, Score = similarity });
        }

        return results.OrderByDescending(r => r.Score).Take(topK).ToList();
    }

    public void RemoveDocument(string documentId) => _chunks.RemoveAll(c => c.DocumentId == documentId);

    public void Clear() => _chunks.Clear();

    public int Count => _chunks.Count;

    public void SaveToDisk()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
        var json = JsonSerializer.Serialize(_chunks, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storagePath, json);
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_storagePath))
            return;

        try
        {
            var json = File.ReadAllText(_storagePath);
            var chunks = JsonSerializer.Deserialize<List<Chunk>>(json);
            if (chunks != null)
            {
                _chunks.Clear();
                _chunks.AddRange(chunks);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading vector store: {ex.Message}");
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length");
        }

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }
}

public class SearchResult
{
    public Chunk Chunk { get; set; } = null!;
    public float Score { get; set; }
}
