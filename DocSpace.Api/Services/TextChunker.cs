namespace DocSpace.Api.Services;

public static class TextChunker
{
    // Simple fixed-size chunking with overlap (chars), tries not to cut mid-word.
    public static List<string> Chunk(string text, int chunkSize = 1800, int overlap = 200)
    {
        text = (text ?? "").Replace("\r\n", "\n").Trim();
        if (text.Length == 0) return new List<string>();

        chunkSize = Math.Clamp(chunkSize, 400, 8000);
        overlap = Math.Clamp(overlap, 0, Math.Min(1000, chunkSize / 2));

        var chunks = new List<string>();
        int start = 0;

        while (start < text.Length)
        {
            int end = Math.Min(text.Length, start + chunkSize);

            // Avoid cutting inside a word when possible
            if (end < text.Length)
            {
                int back = end;
                int minBack = Math.Max(start + 200, start);
                while (back > minBack && !char.IsWhiteSpace(text[back - 1]))
                    back--;

                if (back > minBack) end = back;
            }

            var chunk = text.Substring(start, end - start).Trim();
            if (chunk.Length > 0) chunks.Add(chunk);

            if (end >= text.Length) break;
            start = Math.Max(0, end - overlap);
        }

        return chunks;
    }
}
