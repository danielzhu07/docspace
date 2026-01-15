using DocSpace.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocSpace.Api.Controllers;

[ApiController]
[Route("search")]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db)
    {
        _db = db;
    }

    // GET /search?q=...
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10)
    {
        q = (q ?? "").Trim();
        if (q.Length == 0)
            return BadRequest(new { error = "Query parameter 'q' is required." });

        limit = Math.Clamp(limit, 1, 50);

        // Simple ranking:
        //  - filename match gets higher weight
        //  - content match counts occurrences (cheap approximation)
        // Note: we pull a small candidate set and rank in memory to keep it simple.
        var candidates = await _db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Take(200)
            .Select(d => new { d.Id, d.FileName, d.UploadedAt, d.Content })
            .ToListAsync();

        var qLower = q.ToLowerInvariant();

        int CountOccurrences(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return 0;
            int count = 0, idx = 0;
            while (true)
            {
                idx = text.IndexOf(pattern, idx, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;
                count++;
                idx += pattern.Length;
            }
            return count;
        }

        var results = candidates
            .Select(d =>
            {
                var fileHit = d.FileName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ? 5 : 0;
                var contentHits = CountOccurrences(d.Content ?? "", q);
                var score = fileHit + contentHits;

                // Small snippet around first match
                string snippet = "";
                var pos = (d.Content ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase);
                if (pos >= 0)
                {
                    var start = Math.Max(0, pos - 40);
                    var len = Math.Min((d.Content ?? "").Length - start, 120);
                    snippet = (d.Content ?? "").Substring(start, len).Replace("\n", " ");
                }

                return new
                {
                    id = d.Id,
                    fileName = d.FileName,
                    uploadedAt = d.UploadedAt,
                    score,
                    snippet
                };
            })
            .Where(r => r.score > 0)
            .OrderByDescending(r => r.score)
            .ThenByDescending(r => r.uploadedAt)
            .Take(limit)
            .ToList();

        return Ok(new { query = q, count = results.Count, results });
    }
}
