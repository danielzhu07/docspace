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

    public DocumentsController(AppDbContext db, EmbeddingClient embed)
    {
        _db = db;
        _embed = embed;
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

        var embedding = await _embed.EmbedAsync(content);

        var doc = new Document
        {
            FileName = file.FileName,
            Content = content,
            UploadedAt = DateTimeOffset.UtcNow,
            Embedding = embedding
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

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

        var embedding = await _embed.EmbedAsync(content);

        var doc = new Document
        {
            FileName = fileName,
            Content = content,
            UploadedAt = DateTimeOffset.UtcNow,
            Embedding = embedding
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

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
    // Delete document
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
}
