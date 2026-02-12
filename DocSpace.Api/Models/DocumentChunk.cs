using System;

namespace DocSpace.Api.Models;

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = default!;

    public int ChunkIndex { get; set; }
    public string Content { get; set; } = "";

    // Vector embedding
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
