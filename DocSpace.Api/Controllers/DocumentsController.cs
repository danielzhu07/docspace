using System.Text;
using DocSpace.Api.Data;
using DocSpace.Api.Models;
using DocSpace.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocSpace.Api.Controllers;

// ---------- Request DTOs ----------

public class UploadDocumentRequest
{
    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = default!;
}

public class CreateDocumentRequest
{
    public string FileName { get; set; } = "pasted.txt";
    public string Content { get; set; } = "";
}

// ---------- Controller ----------

[ApiController]
[Route("documents")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmbeddingClient _embed;
    private readonly SemanticSplitter _splitter;

    public DocumentsController(AppDbContext db, EmbeddingClient embed, SemanticSplitter splitter)
    {
        _db = db;
        _embed = embed;
        _splitter = splitter;
    }

    // ==============================
    // POST /documents/upload
    // Upload .txt/.md file
    // ==============================
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request)
    {
        var file = request.File;

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".txt" && ext != ".md")
            return BadRequest(new { error = "Only .txt or .md files are supported." });

        string content;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true))
        {
            content = await reader.ReadToEndAsync();
        }

        content = content.Replace("\r\n", "\n").Trim();
        if (content.Length == 0)
            return BadRequest(new { error = "File is empty." });

        // 1) Create the document (so we have doc.Id)
        var doc = new Document
        {
            FileName = file.FileName,
            Content = content,
            UploadedAt = DateTimeOffset.UtcNow
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        // 2) Semantic divergence chunking -> chunks + embeddings
        await CreateSemanticChunksAsync(doc.Id, content);

        return Ok(new
        {
            id = doc.Id,
            fileName = doc.FileName,
            uploadedAt = doc.UploadedAt,
            charCount = doc.Content.Length
        });
    }

    // ==============================
    // POST /documents
    // Create document from pasted text
    // ==============================
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "Invalid JSON body." });

        var fileName = (request.FileName ?? "").Trim();
        var content = (request.Content ?? "").Replace("\r\n", "\n").Trim();

        if (content.Length == 0)
            return BadRequest(new { error = "Content is required." });

        if (fileName.Length == 0)
            fileName = "pasted.txt";

        // 1) Create the document
        var doc = new Document
        {
            FileName = fileName,
            Content = content,
            UploadedAt = DateTimeOffset.UtcNow
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        // 2) Semantic divergence chunking -> chunks + embeddings
        await CreateSemanticChunksAsync(doc.Id, content);

        return Ok(new
        {
            id = doc.Id,
            fileName = doc.FileName,
            uploadedAt = doc.UploadedAt,
            charCount = doc.Content.Length
        });
    }

    // ==============================
    // GET /documents
    // List documents
    // ==============================
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var docs = await _db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new
            {
                id = d.Id,
                fileName = d.FileName,
                uploadedAt = d.UploadedAt,
                charCount = d.Content.Length
            })
            .ToListAsync();

        return Ok(docs);
    }

    // ==============================
    // GET /documents/{id}
    // Get full document
    // ==============================
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { error = "Document not found." });

        return Ok(new
        {
            id = doc.Id,
            fileName = doc.FileName,
            uploadedAt = doc.UploadedAt,
            content = doc.Content
        });
    }

    // ==============================
    // DELETE /documents/{id}
    // Delete document (chunks cascade delete)
    // ==============================
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null)
            return NotFound(new { error = "Document not found." });

        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ==============================
    // Helper: semantic divergence chunking + store in DocumentChunks
    // ==============================
    private async Task CreateSemanticChunksAsync(Guid documentId, string content)
    {
        // If you re-upload the same docId (not typical), avoid duplicates
        // (safe no-op if none exist)
        var existing = await _db.DocumentChunks.Where(c => c.DocumentId == documentId).ToListAsync();
        if (existing.Count > 0)
        {
            _db.DocumentChunks.RemoveRange(existing);
            await _db.SaveChangesAsync();
        }

        var sentences = _splitter.SplitIntoSentences(content);
        if (sentences.Count == 0)
            sentences = new List<string> { content };

        // Batch embed sentences
        var sentVecsArr = await _embed.EmbedManyAsync(sentences);
        var sentVecs = sentVecsArr.ToList();

        // Find semantic split points + build ranges
        var splitPoints = _splitter.FindSplitPoints(sentVecs, window: 8, percentile: 0.85f);
        var ranges = _splitter.BuildRanges(sentences.Count, splitPoints, minSentences: 3, maxSentences: 20);

        var chunkEntities = new List<DocumentChunk>();
        int idx = 0;

        foreach (var (start, end) in ranges)
        {
            var chunkText = SemanticSplitter.JoinSentences(sentences, start, end);
            if (chunkText.Length == 0) continue;

            var chunkEmb = _splitter.ChunkEmbeddingFromSentences(sentVecs, start, end);

            chunkEntities.Add(new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = idx++,
                Content = chunkText,
                Embedding = chunkEmb
            });
        }

        // Fallback: if semantic splitter produces nothing, store whole doc as one chunk
        if (chunkEntities.Count == 0)
        {
            var emb = await _embed.EmbedAsync(content);
            chunkEntities.Add(new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = 0,
                Content = content,
                Embedding = emb
            });
        }

        _db.DocumentChunks.AddRange(chunkEntities);
        await _db.SaveChangesAsync();
    }
}
