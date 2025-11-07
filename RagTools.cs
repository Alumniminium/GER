using System.ComponentModel;
using ModelContextProtocol.Server;

namespace GER;

[McpServerToolType]
public class RagTools
{
    private readonly RagService _ragService;

    public RagTools(RagService ragService)
    {
        _ragService = ragService;
    }

    [McpServerTool]
    [Description("Index a document into the RAG system. Chunks the document and creates embeddings for retrieval.")]
    public async Task<string> IndexDocument(
        [Description("Unique identifier for the document")] string documentId,
        [Description("Content of the document to index")] string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _ragService.IndexDocumentAsync(documentId, content, null, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            return $"Error indexing document: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search for relevant document chunks based on a query.")]
    public async Task<string> Search(
        [Description("Search query to find relevant document chunks")] string query,
        [Description("Number of top results to return (default: 5)")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _ragService.SearchAsync(query, topK, cancellationToken);

            if (results.Count == 0)
            {
                return "No results found.";
            }

            var output = $"Found {results.Count} results:\n\n";
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                output += $"[{i + 1}] Score: {result.Score:F4}\n";
                output += $"Document: {result.Chunk.DocumentId}\n";
                output += $"Chunk ID: {result.Chunk.Id}\n";
                output += $"Text: {result.Chunk.Text}\n\n";
                output += "---\n\n";
            }

            return output;
        }
        catch (Exception ex)
        {
            return $"Error searching: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Retrieve formatted context for a query. Returns the most relevant document chunks formatted for use in prompts.")]
    public async Task<string> RetrieveContext(
        [Description("Query to retrieve relevant context for")] string query,
        [Description("Number of top results to include in context (default: 5)")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await _ragService.RetrieveContextAsync(query, topK, cancellationToken);
            return context;
        }
        catch (Exception ex)
        {
            return $"Error retrieving context: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Remove a document from the index.")]
    public string RemoveDocument(
        [Description("Document ID to remove")] string documentId)
    {
        try
        {
            _ragService.RemoveDocument(documentId);
            return $"Removed document: {documentId}";
        }
        catch (Exception ex)
        {
            return $"Error removing document: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Clear the entire document index.")]
    public string ClearIndex()
    {
        try
        {
            _ragService.ClearIndex();
            return "Index cleared successfully.";
        }
        catch (Exception ex)
        {
            return $"Error clearing index: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get statistics about the current index.")]
    public string GetStats()
    {
        try
        {
            var count = _ragService.GetDocumentCount();
            return $"Total chunks in index: {count}";
        }
        catch (Exception ex)
        {
            return $"Error getting stats: {ex.Message}";
        }
    }
}
