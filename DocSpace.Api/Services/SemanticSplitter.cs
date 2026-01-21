using System.Text.RegularExpressions;

namespace DocSpace.Api.Services;

public class SemanticSplitter
{
    private static readonly Regex SentenceSplit =
        new Regex(@"(?<=[\.!\?])\s+", RegexOptions.Compiled);

    public List<string> SplitIntoSentences(string text)
    {
        text = (text ?? "").Replace("\r\n", "\n").Trim();
        if (text.Length == 0) return new();

        return SentenceSplit
            .Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    // embeddings are normalized; dot == cosine similarity
    private static float Dot(float[] a, float[] b)
    {
        float sum = 0f;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++) sum += a[i] * b[i];
        return sum;
    }

    // Mean embedding over [start,end), then normalize to unit length
    private static float[] Mean(IReadOnlyList<float[]> vecs, int start, int end)
    {
        int d = vecs[start].Length;
        var m = new float[d];

        for (int i = start; i < end; i++)
            for (int j = 0; j < d; j++)
                m[j] += vecs[i][j];

        int n = end - start;
        for (int j = 0; j < d; j++) m[j] /= n;

        float norm = 0f;
        for (int j = 0; j < d; j++) norm += m[j] * m[j];
        norm = (float)Math.Sqrt(norm);

        if (norm > 0)
            for (int j = 0; j < d; j++) m[j] /= norm;

        return m;
    }

    // sliding window divergence peaks
    public List<int> FindSplitPoints(
        IReadOnlyList<float[]> sentenceEmbeddings,
        int window = 8,
        float percentile = 0.85f)
    {
        int n = sentenceEmbeddings.Count;
        int half = window / 2;
        if (n < window + 2) return new();

        var scores = new List<(int idx, float div)>();

        for (int i = half; i < n - half; i++)
        {
            var left = Mean(sentenceEmbeddings, i - half, i);
            var right = Mean(sentenceEmbeddings, i, i + half);
            float div = 1f - Dot(left, right);
            scores.Add((i, div));
        }

        if (scores.Count == 0) return new();

        var sorted = scores.Select(s => s.div).OrderBy(x => x).ToList();
        int tIndex = (int)Math.Floor(percentile * (sorted.Count - 1));
        float threshold = sorted[Math.Clamp(tIndex, 0, sorted.Count - 1)];

        return scores
            .Where(s => s.div >= threshold)
            .Select(s => s.idx)
            .Distinct()
            .OrderBy(i => i)
            .ToList();
    }

    // Turn split points into sentence ranges [start,end)
    public List<(int start, int end)> BuildRanges(
        int sentenceCount,
        List<int> splitPoints,
        int minSentences = 3,
        int maxSentences = 20)
    {
        var boundaries = new List<int> { 0 };
        boundaries.AddRange(splitPoints.Where(x => x > 0 && x < sentenceCount));
        boundaries.Add(sentenceCount);

        boundaries = boundaries.Distinct().OrderBy(x => x).ToList();

        var ranges = new List<(int start, int end)>();
        for (int i = 0; i < boundaries.Count - 1; i++)
            ranges.Add((boundaries[i], boundaries[i + 1]));

        // merge too-small chunks
        var merged = new List<(int start, int end)>();
        foreach (var r in ranges)
        {
            if (merged.Count == 0) { merged.Add(r); continue; }

            int len = r.end - r.start;
            if (len < minSentences)
            {
                var last = merged[^1];
                merged[^1] = (last.start, r.end);
            }
            else
            {
                merged.Add(r);
            }
        }

        // split too-large chunks
        var finalRanges = new List<(int start, int end)>();
        foreach (var r in merged)
        {
            int len = r.end - r.start;
            if (len <= maxSentences)
            {
                finalRanges.Add(r);
            }
            else
            {
                int s = r.start;
                while (s < r.end)
                {
                    int e = Math.Min(r.end, s + maxSentences);
                    finalRanges.Add((s, e));
                    s = e;
                }
            }
        }

        return finalRanges;
    }

    public static string JoinSentences(List<string> sentences, int start, int end)
        => string.Join(" ", sentences.GetRange(start, end - start)).Trim();

    public float[] ChunkEmbeddingFromSentences(IReadOnlyList<float[]> sentenceEmbeddings, int start, int end)
        => Mean(sentenceEmbeddings, start, end);
}
