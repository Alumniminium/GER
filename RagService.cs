namespace GER;

public class RagService
{
    private readonly OllamaClient _ollamaClient;
    private readonly VectorStore _vectorStore;
    private readonly DocumentChunker _chunker;

    public RagService(OllamaClient ollamaClient, VectorStore vectorStore, DocumentChunker? chunker = null)
    {
        _ollamaClient = ollamaClient;
        _vectorStore = vectorStore;
        _chunker = chunker ?? new DocumentChunker();
    }

    public async Task<string> IndexDocumentAsync(
        string documentId,
        string content,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // Remove existing document if it exists
        _vectorStore.RemoveDocument(documentId);

        // Chunk the document
        var chunks = _chunker.ChunkDocument(content, documentId, metadata);

        if (chunks.Count == 0)
        {
            return $"No chunks created for document {documentId}";
        }

        // Get embeddings for all chunks
        var texts = chunks.Select(c => c.Text).ToList();
        var embeddings = await _ollamaClient.GetEmbeddingsAsync(texts, cancellationToken);

        // Attach embeddings to chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Embedding = embeddings[i];
        }

        // Add to vector store
        _vectorStore.AddChunks(chunks);

        // Persist to disk
        _vectorStore.SaveToDisk();

        return $"Indexed document {documentId} with {chunks.Count} chunks";
    }

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // Get query embedding
        var queryEmbedding = await _ollamaClient.GetEmbeddingAsync(query, cancellationToken);

        // Search vector store
        var results = _vectorStore.Search(queryEmbedding, topK);

        return results;
    }

    public async Task<string> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, topK, cancellationToken);

        if (results.Count == 0)
        {
            return "No relevant context found.";
        }

        var contextParts = results.Select((r, i) =>
            $"[{i + 1}] (score: {r.Score:F4}, doc: {r.Chunk.DocumentId})\n{r.Chunk.Text}"
        );

        return string.Join("\n\n---\n\n", contextParts);
    }

    public void RemoveDocument(string documentId)
    {
        _vectorStore.RemoveDocument(documentId);
        _vectorStore.SaveToDisk();
    }

    public void ClearIndex()
    {
        _vectorStore.Clear();
        _vectorStore.SaveToDisk();
    }

    public int GetDocumentCount()
    {
        return _vectorStore.Count;
    }
}
