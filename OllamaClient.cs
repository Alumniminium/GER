using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GER;

public class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaClient(string baseUrl = "http://localhost:11434", string model = "mxbai-embed-large")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _model = model;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new EmbedRequest { Model = _model, Input = text };

        var response = await _httpClient.PostAsJsonAsync("/api/embed", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken);

        if (result?.Embeddings == null || result.Embeddings.Count == 0)
        {
            throw new InvalidOperationException("No embeddings returned from Ollama");
        }

        return result.Embeddings[0];
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default
    )
    {
        var request = new EmbedRequest { Model = _model, Input = texts.ToArray() };

        var response = await _httpClient.PostAsJsonAsync("/api/embed", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken);

        if (result?.Embeddings == null)
        {
            throw new InvalidOperationException("No embeddings returned from Ollama");
        }

        return result.Embeddings;
    }

    public async Task<string> GenerateResponseAsync(
        string systemPrompt,
        string userPrompt,
        string chatModel,
        CancellationToken cancellationToken = default
    )
    {
        var request = new ChatRequest
        {
            Model = chatModel,
            Messages =
            [
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt },
            ],
            Stream = false,
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);

        if (result?.Message == null)
        {
            throw new InvalidOperationException("No response returned from Ollama");
        }

        return result.Message.Content;
    }

    public async Task<ChatResponse> GenerateResponseWithToolsAsync(
        List<ChatMessage> messages,
        string chatModel,
        List<OllamaTool>? tools = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = new ChatRequest
        {
            Model = chatModel,
            Messages = messages,
            Tools = tools,
            Stream = false,
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);

        if (result?.Message == null)
        {
            throw new InvalidOperationException("No response returned from Ollama");
        }

        return result;
    }

    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        string systemPrompt,
        string userPrompt,
        string chatModel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var request = new ChatRequest
        {
            Model = chatModel,
            Messages =
            [
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt },
            ],
            Stream = true,
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(request),
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var chunk = JsonSerializer.Deserialize<ChatResponse>(line);
            if (chunk?.Message?.Content != null)
            {
                yield return chunk.Message.Content;
            }

            if (chunk?.Done == true)
                break;
        }
    }
}

internal class EmbedRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public object Input { get; set; } = string.Empty; // Can be string or string[]
}

internal class EmbedResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("embeddings")]
    public List<float[]> Embeddings { get; set; } = [];
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("tools")]
    public List<OllamaTool>? Tools { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public ToolFunction Function { get; set; } = new();
}

public class ToolFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public ToolParameters Parameters { get; set; } = new();
}

public class ToolParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, ToolProperty> Properties { get; set; } = [];

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = [];
}

public class ToolProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class ToolCall
{
    [JsonPropertyName("function")]
    public ToolCallFunction Function { get; set; } = new();
}

public class ToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = [];
}
