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

        // Embed the query
        var qVec = await _embed.EmbedAsync(q);

        // Pull recent docs with embeddings
        var docs = await _db.Documents
            .Where(d => d.Embedding != null)
            .OrderByDescending(d => d.UploadedAt)
            .Take(300)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                d.UploadedAt,
                d.Content,
                d.Embedding
            })
            .ToListAsync();

        static float Dot(float[] a, float[] b)
        {
            float sum = 0f;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
                sum += a[i] * b[i];
            return sum;
        }

        // embeddings are normalized → dot product == cosine similarity
        var results = docs
            .Select(d => new
            {
                id = d.Id,
                fileName = d.FileName,
                uploadedAt = d.UploadedAt,
                score = Dot(d.Embedding!, qVec),
                snippet = d.Content.Length > 200
                    ? d.Content.Substring(0, 200)
                    : d.Content
            })
            .OrderByDescending(r => r.score)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            query = q,
            count = results.Count,
            results
        });
    }
}
