using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using GER;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "mxbai-embed-large";
var storagePath = Environment.GetEnvironmentVariable("GER_STORAGE_PATH") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ger", "index.json");

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register services
builder.Services.AddSingleton(new OllamaClient(ollamaUrl, ollamaModel));
builder.Services.AddSingleton(new VectorStore(storagePath));
builder.Services.AddSingleton<DocumentChunker>();
builder.Services.AddSingleton<RagService>();
builder.Services.AddSingleton<RagTools>();

// Configure MCP server with HTTP transport
builder.Services
    .AddMcpServer()
    .WithToolsFromAssembly();

var app = builder.Build();

// Log startup info
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("GER RAG MCP Server starting...");
logger.LogInformation("Ollama URL: {OllamaUrl}", ollamaUrl);
logger.LogInformation("Ollama Model: {OllamaModel}", ollamaModel);
logger.LogInformation("Storage Path: {StoragePath}", storagePath);

// Map MCP endpoint with Streamable HTTP transport
app.MapMcp("/mcp");

// Add a simple health check endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "GER RAG MCP Server",
    version = "1.0.0",
    transport = "Streamable HTTP",
    endpoints = new { mcp = "/mcp" }
}));

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
logger.LogInformation("Server listening on http://localhost:{Port}", port);
logger.LogInformation("MCP endpoint available at http://localhost:{Port}/mcp", port);

app.Run($"http://0.0.0.0:{port}");
