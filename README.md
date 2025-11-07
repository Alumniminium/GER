# GER - Grid Enhanced Retrieval

A Model Context Protocol (MCP) server that provides Retrieval-Augmented Generation (RAG) capabilities using Ollama embeddings.

## Features

- Document indexing with automatic chunking
- Vector similarity search using cosine similarity
- Persistent storage of document embeddings
- Integration with Ollama for embeddings (mxbai-embed-large)
- **Streamable HTTP transport** for remote MCP connectivity
- MCP protocol support for easy integration with Claude and other AI tools

## Requirements

- .NET 9.0 or later
- Ollama running locally with mxbai-embed-large model

## Setup

1. Install Ollama and pull the embedding model:
```bash
ollama pull mxbai-embed-large
```

2. Build the project:
```bash
dotnet build
```

3. Run the server:
```bash
dotnet run
```

The server will start on `http://localhost:5000` by default.

## Configuration

Environment variables:

- `PORT` - HTTP server port (default: 5000)
- `OLLAMA_URL` - Ollama API URL (default: http://localhost:11434)
- `OLLAMA_MODEL` - Ollama embedding model (default: mxbai-embed-large)
- `GER_STORAGE_PATH` - Path to store the vector index (default: ~/.ger/index.json)

## MCP Tools

The server exposes the following tools via MCP:

### IndexDocument
Index a document into the RAG system.

Parameters:
- `documentId` (string): Unique identifier for the document
- `content` (string): Content of the document to index

### Search
Search for relevant document chunks.

Parameters:
- `query` (string): Search query
- `topK` (int, optional): Number of results to return (default: 5)

### RetrieveContext
Retrieve formatted context for use in prompts.

Parameters:
- `query` (string): Query to retrieve context for
- `topK` (int, optional): Number of results to include (default: 5)

### RemoveDocument
Remove a document from the index.

Parameters:
- `documentId` (string): Document ID to remove

### ClearIndex
Clear the entire document index.

### GetStats
Get statistics about the current index.

## Usage with Claude Desktop

Add to your Claude Desktop configuration for HTTP transport:

```json
{
  "mcpServers": {
    "ger": {
      "url": "http://localhost:5000/mcp",
      "transport": "streamable-http"
    }
  }
}
```

Make sure the server is running first with `dotnet run`.

## Architecture

- **OllamaClient**: Handles communication with Ollama API for embeddings
- **DocumentChunker**: Splits documents into overlapping chunks
- **VectorStore**: Manages document chunks and performs similarity search
- **RagService**: Coordinates indexing and retrieval operations
- **RagTools**: Exposes MCP tools for external integration
- **Program**: MCP server entry point with dependency injection

## Example

```csharp
// Index a document
await IndexDocument("doc1", "This is a sample document about AI and machine learning.");

// Search for relevant chunks
var results = await Search("machine learning", topK: 3);

// Retrieve formatted context
var context = await RetrieveContext("What is AI?", topK: 5);
```
