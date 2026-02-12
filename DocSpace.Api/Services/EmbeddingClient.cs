using System.Net.Http.Json;

namespace DocSpace.Api.Services;

public class EmbeddingClient
{
    private readonly HttpClient _http;

    public EmbeddingClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var res = await _http.PostAsJsonAsync("/embed", new { text });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<EmbedResponse>();
        if (body?.embedding is null) throw new Exception("No embedding returned.");

        return body.embedding.Select(x => (float)x).ToArray();
    }

    private sealed class EmbedResponse
    {
        // matches Python: {"embedding": [...]}
        public double[]? embedding { get; set; }
    }
    
    //Embedding one request per sentence is inefficient, so we provide a batch method
    public async Task<float[][]> EmbedManyAsync(List<string> texts)
    {
        var res = await _http.PostAsJsonAsync("/embed_batch", new { texts });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<EmbedBatchResponse>();
        if (body?.embeddings is null)
            throw new Exception("No embeddings returned.");

        return body.embeddings
            .Select(v => v.Select(x => (float)x).ToArray())
            .ToArray();
    }

    private sealed class EmbedBatchResponse
    {
        public double[][]? embeddings { get; set; }
    }

}
