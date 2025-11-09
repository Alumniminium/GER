using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GER;

public class RagService(
    OllamaClient ollamaClient,
    VectorStore vectorStore,
    ILogger<RagService> logger,
    string chatModel = "qwen3:1.7b",
    DocumentChunker? chunker = null
)
{
    private readonly DocumentChunker _chunker = chunker ?? new DocumentChunker();
    private readonly string _chatModel = chatModel;
    private readonly List<(string DocumentId, string Text)> _usedSources = [];

    public async Task<string> IndexDocumentAsync(
        string documentId,
        string content,
        CancellationToken cancellationToken = default
    )
    {
        // Remove existing document if it exists
        vectorStore.RemoveDocument(documentId);

        // Chunk the document
        var chunks = _chunker.ChunkDocument(content, documentId);

        if (chunks.Count == 0)
        {
            return $"No chunks created for document {documentId}";
        }

        // Get embeddings for all chunks
        var texts = chunks.Select(c => c.Text).ToList();
        var embeddings = await ollamaClient.GetEmbeddingsAsync(texts, cancellationToken);

        // Attach embeddings to chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Embedding = embeddings[i];
        }

        // Add to vector store
        vectorStore.AddChunks(chunks);

        // Persist to disk
        vectorStore.SaveToDisk();

        return $"Indexed document {documentId} with {chunks.Count} chunks";
    }

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        // Get query embedding
        var queryEmbedding = await ollamaClient.GetEmbeddingAsync(query, cancellationToken);

        // Search vector store
        var results = vectorStore.Search(queryEmbedding, topK);

        return results;
    }

    public async Task<string> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        var results = await SearchAsync(query, topK, cancellationToken);

        if (results.Count == 0)
        {
            return "No relevant context found.";
        }

        var contextParts = results.Select(
            (r, i) => $"[{i + 1}] (score: {r.Score:F4}, doc: {r.Chunk.DocumentId})\n{r.Chunk.Text}"
        );

        return string.Join("\n\n---\n\n", contextParts);
    }

    public void RemoveDocument(string documentId)
    {
        vectorStore.RemoveDocument(documentId);
        vectorStore.SaveToDisk();
    }

    public void ClearIndex()
    {
        vectorStore.Clear();
        vectorStore.SaveToDisk();
    }

    public int GetDocumentCount() => vectorStore.Count;

    public string FormatSearchResults(List<SearchResult> results)
    {
        if (results.Count == 0)
            return "No results found.";

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

    // Agentic RAG methods
    private void TrackSourcesFromToolResult(string toolResult)
    {
        // Parse document IDs from tool output
        // Format: "Document: <documentId>"
        var lines = toolResult.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("Document: "))
            {
                var documentId = line.Substring("Document: ".Length).Trim();
                if (!string.IsNullOrEmpty(documentId) && !_usedSources.Any(s => s.DocumentId == documentId))
                {
                    _usedSources.Add((documentId, string.Empty)); // We don't need the text for citation
                }
            }
        }
    }

    private async Task<string> SearchTool(string query, int topK, CancellationToken cancellationToken)
    {
        var results = await SearchAsync(query, topK, cancellationToken);
        return FormatSearchResults(results);
    }

    private async Task<string> ExecuteToolCall(ToolCall toolCall, CancellationToken cancellationToken)
    {
        var functionName = toolCall.Function.Name;
        var arguments = toolCall.Function.Arguments;

        try
        {
            var query = GetStringFromArgument(arguments, "query");
            var topK = GetIntFromArgument(arguments, "topK", 5);

            return functionName switch
            {
                "search" => await SearchTool(query, topK, cancellationToken),
                "retrieve_context" => await RetrieveContextAsync(query, topK, cancellationToken),
                _ => $"Unknown tool: {functionName}",
            };
        }
        catch (Exception ex)
        {
            return $"Error executing tool {functionName}: {ex.Message}";
        }
    }

    public async Task<string> AskAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("AskAsync called with query: {Query}, topK: {TopK}", query, topK);

            // Clear sources from previous queries
            _usedSources.Clear();

            var systemPrompt = SystemPromptManager.GetSystemPrompt();
            var tools = RagTools.GetRagTools();
            logger.LogInformation(
                "System prompt length: {Length}, Available tools: {ToolCount}",
                systemPrompt.Length,
                tools.Count
            );

            var messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = query },
            };

            const int maxIterations = 5;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                logger.LogInformation(
                    "Iteration {Iteration}: Calling LLM with {MessageCount} messages",
                    iteration,
                    messages.Count
                );

                var result = await ollamaClient.GenerateResponseWithToolsAsync(
                    messages,
                    _chatModel,
                    tools,
                    cancellationToken
                );

                messages.Add(result.Message!);

                // Check if model wants to use tools
                if (result.Message!.ToolCalls != null && result.Message.ToolCalls.Count > 0)
                {
                    logger.LogInformation("LLM requested {ToolCallCount} tool calls", result.Message.ToolCalls.Count);

                    // Execute all tool calls
                    foreach (var toolCall in result.Message.ToolCalls)
                    {
                        logger.LogInformation(
                            "Executing tool: {ToolName} with arguments: {Args}",
                            toolCall.Function.Name,
                            JsonSerializer.Serialize(toolCall.Function.Arguments)
                        );

                        var toolResult = await ExecuteToolCall(toolCall, cancellationToken);
                        logger.LogInformation(
                            "Tool {ToolName} returned: {Result}",
                            toolCall.Function.Name,
                            toolResult.Length > 200 ? toolResult.Substring(0, 200) + "..." : toolResult
                        );

                        // Track document sources mentioned in search/retrieve_context results
                        TrackSourcesFromToolResult(toolResult);

                        messages.Add(new ChatMessage { Role = "tool", Content = toolResult });
                    }
                    continue; // Continue the loop to get final response
                }

                logger.LogInformation("LLM did not request tool calls, returning final response");

                // No more tool calls, append citations and return
                var response = result.Message.Content;

                if (_usedSources.Count > 0)
                {
                    response += "\n\n---\n**Sources:**\n";
                    for (int i = 0; i < _usedSources.Count; i++)
                    {
                        var source = _usedSources[i];
                        response += $"\n[{i + 1}] Document: {source.DocumentId}";
                    }
                }

                return response;
            }

            return "Error: Max iterations reached without final answer";
        }
        catch (Exception ex)
        {
            return $"Error processing query: {ex.Message}";
        }
    }

    public static int GetIntFromArgument(Dictionary<string, object> arguments, string key, int defaultValue)
    {
        if (!arguments.TryGetValue(key, out var value))
            return defaultValue;

        return value switch
        {
            JsonElement je => je.GetInt32(),
            int i => i,
            _ => Convert.ToInt32(value),
        };
    }

    public static string GetStringFromArgument(
        Dictionary<string, object> arguments,
        string key,
        string defaultValue = ""
    )
    {
        if (!arguments.TryGetValue(key, out var value))
            return defaultValue;

        return value switch
        {
            JsonElement je => je.GetString() ?? defaultValue,
            string s => s,
            _ => value.ToString() ?? defaultValue,
        };
    }
}
