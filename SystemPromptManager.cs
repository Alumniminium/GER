using System.ComponentModel;
using ModelContextProtocol.Server;

namespace GER;

/// <summary>
/// Manages the system prompt for the agentic RAG system.
/// This service maintains the system prompt state and provides
/// thread-safe access for reading and updating it.
/// </summary>
public static class SystemPromptManager
{
    [McpServerResourceType]
    public class SystemPromptResource()
    {
        [McpServerResource(UriTemplate = "ger://config/system-prompt", Name = "System Prompt", MimeType = "text/plain")]
        [Description("The current system prompt used by the agentic RAG system")]
        public static string GetSystemPrompt() => SystemPromptManager.GetSystemPrompt();
    }

    private static string _systemPrompt = DefaultSystemPrompt;

    public const string DefaultSystemPrompt =
        @"You are a helpful AI assistant with access to a knowledge base.
Your task is to answer the user's question based on the provided context from the knowledge base.

Instructions:
- Use ONLY the information provided in the search results to answer the question
- If the search results don't contain enough information, acknowledge this
- Be concise but thorough
- If you're making inferences, clearly state them as such
- Do NOT add citations or sources - they will be automatically appended
- If no results were found, tell the user the knowledge base doesn't have information on this topic";

    /// <summary>
    /// Get the current system prompt.
    /// </summary>
    public static string GetSystemPrompt() => _systemPrompt;

    /// <summary>
    /// Update the system prompt.
    /// </summary>
    /// <param name="newPrompt">The new system prompt to use. If null or whitespace, resets to default.</param>
    public static void SetSystemPrompt(string newPrompt) => _systemPrompt = string.IsNullOrWhiteSpace(newPrompt) ? DefaultSystemPrompt : newPrompt;

    /// <summary>
    /// Reset the system prompt to the default.
    /// </summary>
    public static void ResetToDefault() => _systemPrompt = DefaultSystemPrompt;
}
