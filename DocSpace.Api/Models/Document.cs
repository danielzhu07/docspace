using System;
using System.Collections.Generic;

namespace DocSpace.Api.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    // Legacy single embedding (we will remove later)
    public float[] Embedding { get; set; } = Array.Empty<float>();

    // NEW
    public List<DocumentChunk> Chunks { get; set; } = new();
}
