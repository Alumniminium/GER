using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using GER;

// Configuration
var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "mxbai-embed-large";
var chatModel = Environment.GetEnvironmentVariable("OLLAMA_CHAT_MODEL") ?? "qwen3:1.7b";
var storagePath = Environment.GetEnvironmentVariable("GER_STORAGE_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ger", "index.json");

// Check if stdio mode is requested
var useStdio = args.Contains("--stdio") || args.Contains("-s");

if (useStdio)
{
    // Stdio mode
    var builder = Host.CreateApplicationBuilder(args);

    // Configure logging to stderr for stdio mode
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    // Register services
    var ollamaClient = new OllamaClient(ollamaUrl, ollamaModel);
    builder.Services.AddSingleton(ollamaClient);
    builder.Services.AddSingleton(new VectorStore(storagePath));
    builder.Services.AddSingleton<DocumentChunker>();
    builder.Services.AddSingleton(sp => new RagService(
        ollamaClient,
        sp.GetRequiredService<VectorStore>(),
        sp.GetRequiredService<ILogger<RagService>>(),
        chatModel));
    builder.Services.AddSingleton<RagTools>();
    builder.Services.AddSingleton<SystemPromptManager.SystemPromptResource>();

    // Configure MCP server with stdio transport
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly()
        .WithResourcesFromAssembly();

    var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("GER - Grid Enhanced Retrieval starting in stdio mode...");
    logger.LogInformation("Ollama URL: {OllamaUrl}", ollamaUrl);
    logger.LogInformation("Ollama Embedding Model: {OllamaModel}", ollamaModel);
    logger.LogInformation("Ollama Chat Model: {ChatModel}", chatModel);
    logger.LogInformation("Storage Path: {StoragePath}", storagePath);

    await host.RunAsync();
}
else
{
    // HTTP mode (default)
    var builder = WebApplication.CreateBuilder(args);

    // Configure logging
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    // Register services
    var ollamaClient = new OllamaClient(ollamaUrl, ollamaModel);
    builder.Services.AddSingleton(ollamaClient);
    builder.Services.AddSingleton(new VectorStore(storagePath));
    builder.Services.AddSingleton<DocumentChunker>();
    builder.Services.AddSingleton(sp => new RagService(
        ollamaClient,
        sp.GetRequiredService<VectorStore>(),
        sp.GetRequiredService<ILogger<RagService>>(),
        chatModel));
    builder.Services.AddSingleton<RagTools>();
    builder.Services.AddSingleton<SystemPromptManager.SystemPromptResource>();

    // Configure MCP server with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly()
        .WithResourcesFromAssembly();

    var app = builder.Build();

    // Log startup info
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("GER - Grid Enhanced Retrieval starting in HTTP mode...");
    logger.LogInformation("Ollama URL: {OllamaUrl}", ollamaUrl);
    logger.LogInformation("Ollama Embedding Model: {OllamaModel}", ollamaModel);
    logger.LogInformation("Ollama Chat Model: {ChatModel}", chatModel);
    logger.LogInformation("Storage Path: {StoragePath}", storagePath);

    // Map MCP endpoint with Streamable HTTP transport
    app.MapMcp("/mcp");

    // Add a simple health check endpoint
    app.MapGet("/", () => Results.Ok(new
    {
        service = "GER - Grid Enhanced Retrieval",
        version = "1.0.0",
        transport = "Streamable HTTP",
        endpoints = new { mcp = "/mcp" }
    }));

    var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
    logger.LogInformation("Server listening on http://localhost:{Port}", port);
    logger.LogInformation("MCP endpoint available at http://localhost:{Port}/mcp", port);

    app.Run($"http://0.0.0.0:{port}");
}

// Make Program accessible for testing
public partial class Program { }
