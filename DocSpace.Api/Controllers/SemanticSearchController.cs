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
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10, [FromQuery] float minScore = 0.25f)
    {
        q = (q ?? "").Trim();
        if (q.Length == 0)
            return BadRequest(new { error = "Query parameter 'q' is required." });

        limit = Math.Clamp(limit, 1, 50);

        // Embed query once
        var qVec = await _embed.EmbedAsync(q);

        static float Dot(float[] a, float[] b)
        {
            float s = 0f;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) s += a[i] * b[i];
            return s;
        }

        // Pull recent chunks + their parent document metadata
        // (No navigation properties needed; explicit join)
        var rows = await (
            from c in _db.DocumentChunks
            join d in _db.Documents on c.DocumentId equals d.Id
            orderby d.UploadedAt descending
            select new
            {
                DocumentId = d.Id,
                d.FileName,
                d.UploadedAt,
                c.ChunkIndex,
                c.Content,
                c.Embedding
            }
        )
        .Take(5000) // fine for your current scale
        .ToListAsync();

        // Score each chunk, take best chunk per document
        var bestPerDoc = rows
            .Select(r => new
            {
                id = r.DocumentId,
                fileName = r.FileName,
                uploadedAt = r.UploadedAt,
                chunkIndex = r.ChunkIndex,
                score = Dot(r.Embedding, qVec),
                snippet = r.Content.Length > 240 ? r.Content.Substring(0, 240) : r.Content
            })
            .OrderByDescending(x => x.score)
            .GroupBy(x => x.id)
            .Select(g => g.First()) // because already sorted by score
            .Where(x => x.score >= minScore) //min similarity score threshold
            .OrderByDescending(x => x.score)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            query = q,
            minScore,
            count = bestPerDoc.Count,
            results = bestPerDoc
        });
    }
}
