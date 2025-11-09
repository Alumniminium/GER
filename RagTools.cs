using System.ComponentModel;
using ModelContextProtocol.Server;

namespace GER;

[McpServerToolType]
public class RagTools(RagService ragService)
{
    public static List<OllamaTool> GetRagTools() => [
            new OllamaTool
            {
                Function = new ToolFunction
                {
                    Name = "search",
                    Description = "Search for relevant document chunks based on a query.",
                    Parameters = new ToolParameters
                    {
                        Properties = new Dictionary<string, ToolProperty>
                        {
                            ["query"] = new ToolProperty
                            {
                                Type = "string",
                                Description = "Search query to find relevant document chunks",
                            },
                            ["topK"] = new ToolProperty
                            {
                                Type = "integer",
                                Description = "Number of top results to return (default: 5)",
                            },
                        },
                        Required = ["query"],
                    },
                },
            },
            new OllamaTool
            {
                Function = new ToolFunction
                {
                    Name = "retrieve_context",
                    Description =
                        "Retrieve formatted context for a query. Returns the most relevant document chunks formatted for use in prompts.",
                    Parameters = new ToolParameters
                    {
                        Properties = new Dictionary<string, ToolProperty>
                        {
                            ["query"] = new ToolProperty
                            {
                                Type = "string",
                                Description = "Query to retrieve relevant context for",
                            },
                            ["topK"] = new ToolProperty
                            {
                                Type = "integer",
                                Description = "Number of top results to include in context (default: 5)",
                            },
                        },
                        Required = ["query"],
                    },
                },
            },
        ];

    [McpServerTool]
    [Description(
        "Index a document into the RAG system. Chunks the document and creates embeddings for retrieval. Provide either content directly or a filePath to read from."
    )]
    public async Task<string> IndexDocument(
        [Description("Unique identifier for the document")] string documentId,
        [Description("Content of the document to index (optional if filePath is provided)")] string? content = null,
        [Description("Path to a file to index (optional if content is provided)")] string? filePath = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            string documentContent;

            // Validate that at least one source is provided
            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(filePath))
            {
                return "Error: Either 'content' or 'filePath' must be provided.";
            }

            // If filePath is provided, read the file
            if (!string.IsNullOrEmpty(filePath))
            {
                if (!File.Exists(filePath))
                    return $"Error: File not found at path: {filePath}";

                documentContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            else
            {
                documentContent = content!;
            }

            var result = await ragService.IndexDocumentAsync(documentId, documentContent, cancellationToken);
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
        [Description("Number of top results to return (default: 2)")] int topK = 2,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var results = await ragService.SearchAsync(query, topK, cancellationToken);
            return ragService.FormatSearchResults(results);
        }
        catch (Exception ex)
        {
            return $"Error searching: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "Retrieve formatted context for a query. Returns the most relevant document chunks formatted for use in prompts."
    )]
    public async Task<string> RetrieveContext(
        [Description("Query to retrieve relevant context for")] string query,
        [Description("Number of top results to include in context (default: 2)")] int topK = 2,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var context = await ragService.RetrieveContextAsync(query, topK, cancellationToken);
            return context;
        }
        catch (Exception ex)
        {
            return $"Error retrieving context: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Remove a document from the index.")]
    public string RemoveDocument([Description("Document ID to remove")] string documentId)
    {
        try
        {
            ragService.RemoveDocument(documentId);
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
            ragService.ClearIndex();
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
            var count = ragService.GetDocumentCount();
            return $"Total chunks in index: {count}";
        }
        catch (Exception ex)
        {
            return $"Error getting stats: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "Ask the RAG agent a question. The agent will search the knowledge base, retrieve relevant context, and generate a comprehensive answer using AI."
    )]
    public async Task<string> AskAgent(
        [Description("The question to ask the RAG agent")] string query,
        [Description("Number of document chunks to retrieve for context (default: 2)")] int topK = 2,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await ragService.AskAsync(query, topK, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            return $"Error asking agent: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "Update the system prompt used by the agentic RAG system. This changes how the agent responds to queries. Pass null or empty string to reset to default."
    )]
    public static string SetSystemPrompt(
        [Description("The new system prompt to use. Leave empty to reset to default.")] string? newPrompt = null
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newPrompt))
            {
                SystemPromptManager.ResetToDefault();
                return "System prompt reset to default.";
            }

            SystemPromptManager.SetSystemPrompt(newPrompt);
            return $"System prompt updated successfully. New prompt length: {newPrompt.Length} characters.";
        }
        catch (Exception ex)
        {
            return $"Error setting system prompt: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get the current system prompt used by the agentic RAG system.")]
    public static string GetSystemPrompt()
    {
        try
        {
            return SystemPromptManager.GetSystemPrompt();
        }
        catch (Exception ex)
        {
            return $"Error getting system prompt: {ex.Message}";
        }
    }
}
