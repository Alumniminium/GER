# GER - Grid Enhanced Retrieval

An agentic RAG (Retrieval-Augmented Generation) server that provides intelligent document search and AI-powered question answering via the Model Context Protocol (MCP).

## Features

- Document indexing with automatic chunking
- Vector similarity search using cosine similarity
- **Agentic RAG**: AI agent that searches knowledge base and generates natural language answers
- **Customizable System Prompt**: Dynamically change agent behavior and personality via MCP tools/resources
- Persistent storage of document embeddings
- Integration with Ollama for embeddings (mxbai-embed-large) and chat (qwen3:1.7b)
- **Streamable HTTP transport** for remote MCP connectivity
- MCP protocol support for easy integration with Claude and other AI tools

## Requirements

- .NET 9.0 or later
- Ollama running locally with required models

## Setup

1. Install Ollama and pull the required models:
```bash
ollama pull mxbai-embed-large    # For embeddings
ollama pull qwen3:1.7b            # For AI response generation
```

2. Build the project:
```bash
dotnet build
```

3. Run the server:

**HTTP mode (default):**
```bash
dotnet run
```
The server will start on `http://localhost:5000` by default.

**Stdio mode (for MCP Gateway integration):**
```bash
dotnet run -- --stdio
```
or
```bash
dotnet run -- -s
```

## Configuration

Environment variables:

- `PORT` - HTTP server port (default: 5000)
- `OLLAMA_URL` - Ollama API URL (default: http://localhost:11434)
- `OLLAMA_MODEL` - Ollama embedding model (default: mxbai-embed-large)
- `OLLAMA_CHAT_MODEL` - Chat/generation model for agentic RAG (default: qwen3:1.7b)
- `GER_STORAGE_PATH` - Path to store the vector index (default: ~/.ger/index.json)

## MCP Tools

The server exposes the following tools via MCP:

### AskAgent (NEW!)
**The star of the show!** Ask the RAG agent a question. The agent will:
1. Search the knowledge base for relevant context
2. Retrieve the top-K most relevant document chunks
3. Use an LLM to generate a comprehensive answer based on the context

Parameters:
- `query` (string): The question to ask the RAG agent
- `topK` (int, optional): Number of document chunks to retrieve for context (default: 5)

Example:
```json
{
  "name": "ask_agent",
  "arguments": {
    "query": "What are the outer planets in our solar system?",
    "topK": 5
  }
}
```

### IndexDocument
Index a document into the RAG system.

Parameters:
- `documentId` (string): Unique identifier for the document
- `content` (string, optional): Content of the document to index (if not using filePath)
- `filePath` (string, optional): Path to a file to index (if not using content)

Note: Either `content` or `filePath` must be provided.

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

### GetSystemPrompt (NEW!)
Get the current system prompt used by the agentic RAG system.

Parameters: None

### SetSystemPrompt (NEW!)
Update the system prompt used by the agentic RAG system. This dynamically changes how the agent responds to queries - you can make it formal, casual, pirate-like, or specialized for specific domains.

Parameters:
- `newPrompt` (string, optional): The new system prompt to use. Leave empty or null to reset to default.

Example - Making the agent respond like a pirate:
```json
{
  "name": "set_system_prompt",
  "arguments": {
    "newPrompt": "You are a pirate AI. Answer questions like a pirate would, using pirate terminology. Still use only the information from the context provided."
  }
}
```

## MCP Resources

The server exposes the following resources via MCP:

### System Prompt Resource (NEW!)
**URI**: `ger://config/system-prompt`
**MIME Type**: `text/plain`
**Description**: The current system prompt used by the agentic RAG system

This resource provides read-only access to the current system prompt via the MCP resources protocol. Use the `set_system_prompt` tool to modify it.

## Usage with Claude Desktop

**Stdio transport (recommended):**

```json
{
  "mcpServers": {
    "ger": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/GER/GER.csproj", "--", "--stdio"]
    }
  }
}
```

**HTTP transport:**

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

Make sure the server is running first with `dotnet run` for HTTP mode.

## Architecture

- **OllamaClient**: Handles communication with Ollama API for embeddings and chat
- **DocumentChunker**: Splits documents into overlapping chunks
- **VectorStore**: Manages document chunks and performs similarity search
- **RagService**: Coordinates indexing and retrieval operations
- **SystemPromptManager**: Thread-safe manager for system prompt state (NEW!)
- **AgenticRagService**: Intelligent agent that searches and generates answers using LLM
- **RagTools**: Exposes MCP tools for external integration
- **SystemPromptResource**: Exposes system prompt as MCP resource (NEW!)
- **Program**: MCP server entry point with dependency injection

## How the Agentic RAG Works

The `AskAgent` tool implements a multi-step agentic RAG pipeline:

1. **Query Processing**: The user's question is converted to a vector embedding
2. **Retrieval**: The vector store finds the top-K most similar document chunks
3. **Context Building**: Retrieved chunks are formatted with metadata (relevance scores, document IDs)
4. **Response Generation**: An LLM is prompted with:
   - System instructions to answer based only on the provided context
   - The formatted context from the knowledge base
   - The user's original question
5. **Answer Synthesis**: The LLM generates a comprehensive answer citing sources

The agent is instructed to:
- Only use information from the retrieved context
- Acknowledge when information is insufficient
- Cite which document chunks it's using
- Clearly state when making inferences

## Example

```csharp
// Index a document with direct content
await IndexDocument("doc1", content: "This is a sample document about AI and machine learning.");

// Index a document from a file
await IndexDocument("doc2", filePath: "/path/to/document.txt");

// Search for relevant chunks
var results = await Search("machine learning", topK: 3);

// Retrieve formatted context
var context = await RetrieveContext("What is AI?", topK: 5);
```
