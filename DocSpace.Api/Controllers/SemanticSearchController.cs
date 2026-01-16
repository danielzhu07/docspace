using DocSpace.Api.Data;
using DocSpace.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocSpace.Api.Controllers;

[ApiController]
[Route("search/semantic")]
public class SemanticSearchController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmbeddingClient _embed;

    public SemanticSearchController(AppDbContext db, EmbeddingClient embed)
    {
        _db = db;
        _embed = embed;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10)
    {
        q = (q ?? "").Trim();
        if (q.Length == 0)
            return BadRequest(new { error = "Query parameter 'q' is required." });

        limit = Math.Clamp(limit, 1, 50);

        var qVec = await _embed.EmbedAsync(q);

        static float Dot(float[] a, float[] b)
        {
            float s = 0f;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) s += a[i] * b[i];
            return s;
        }

        // Load chunk embeddings + parent doc metadata
        var chunks = await _db.DocumentChunks
            .Include(c => c.Document)
            .OrderByDescending(c => c.Document.UploadedAt)
            .Take(5000) // fine for your scale
            .Select(c => new
            {
                c.DocumentId,
                c.ChunkIndex,
                c.Content,
                c.Embedding,
                FileName = c.Document.FileName,
                UploadedAt = c.Document.UploadedAt
            })
            .ToListAsync();

        // Rank chunks, then keep best chunk per document
        var bestPerDoc = chunks
            .Select(c => new
            {
                id = c.DocumentId,
                fileName = c.FileName,
                uploadedAt = c.UploadedAt,
                score = Dot(c.Embedding, qVec),
                snippet = c.Content.Length > 240 ? c.Content.Substring(0, 240) : c.Content
            })
            .OrderByDescending(x => x.score)
            .GroupBy(x => x.id)
            .Select(g => g.First()) // best chunk for that doc (because already sorted)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .ToList();

        return Ok(new { query = q, count = bestPerDoc.Count, results = bestPerDoc });
    }
}
