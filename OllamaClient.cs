using System.Net.Http.Json;
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
        var request = new EmbedRequest
        {
            Model = _model,
            Input = text
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embed", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken);

        if (result?.Embeddings == null || result.Embeddings.Count == 0)
        {
            throw new InvalidOperationException("No embeddings returned from Ollama");
        }

        return result.Embeddings[0];
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var request = new EmbedRequest
        {
            Model = _model,
            Input = texts.ToArray()
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embed", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken);

        if (result?.Embeddings == null)
        {
            throw new InvalidOperationException("No embeddings returned from Ollama");
        }

        return result.Embeddings;
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
    public List<float[]> Embeddings { get; set; } = new();
}
