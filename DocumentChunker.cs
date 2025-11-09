namespace GER;

public class DocumentChunker(int chunkSize = 512, int chunkOverlap = 128)
{
    public List<Chunk> ChunkDocument(string content, string documentId)
    {
        var chunks = new List<Chunk>();
        var sentences = SplitIntoSentences(content);

        var currentChunk = new List<string>();
        var currentLength = 0;
        var chunkIndex = 0;

        foreach (var sentence in sentences)
        {
            var sentenceLength = sentence.Length;

            if (currentLength + sentenceLength > chunkSize && currentChunk.Count > 0)
            {
                // Create chunk from current sentences
                var chunkText = string.Join(" ", currentChunk);
                chunks.Add(
                    new Chunk
                    {
                        Id = $"{documentId}_chunk_{chunkIndex}",
                        DocumentId = documentId,
                        Text = chunkText,
                        Index = chunkIndex,
                    }
                );

                chunkIndex++;

                // Keep overlap sentences
                var overlapSentences = new List<string>();
                var overlapLength = 0;

                for (int i = currentChunk.Count - 1; i >= 0; i--)
                {
                    if (overlapLength + currentChunk[i].Length <= chunkOverlap)
                    {
                        overlapSentences.Insert(0, currentChunk[i]);
                        overlapLength += currentChunk[i].Length;
                    }
                    else
                    {
                        break;
                    }
                }

                currentChunk = overlapSentences;
                currentLength = overlapLength;
            }

            currentChunk.Add(sentence);
            currentLength += sentenceLength + 1; // +1 for space
        }

        // Add remaining chunk
        if (currentChunk.Count > 0)
        {
            var chunkText = string.Join(" ", currentChunk);
            chunks.Add(
                new Chunk
                {
                    Id = $"{documentId}_chunk_{chunkIndex}",
                    DocumentId = documentId,
                    Text = chunkText,
                    Index = chunkIndex,
                }
            );
        }

        return chunks;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting - can be improved with better NLP
        var sentences = new List<string>();
        var sentenceEndings = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };

        var currentSentence = "";
        var i = 0;

        while (i < text.Length)
        {
            currentSentence += text[i];

            foreach (var ending in sentenceEndings)
            {
                if (i + ending.Length <= text.Length && text.Substring(i, ending.Length) == ending)
                {
                    currentSentence += ending.Substring(1);
                    sentences.Add(currentSentence.Trim());
                    currentSentence = "";
                    i += ending.Length - 1;
                    break;
                }
            }

            i++;
        }

        if (!string.IsNullOrWhiteSpace(currentSentence))
        {
            sentences.Add(currentSentence.Trim());
        }

        return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }
}

public class Chunk
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Index { get; set; }
    public float[]? Embedding { get; set; }
}
