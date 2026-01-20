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

    private static float Dot(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }

    private static float[] Mean(IReadOnlyList<float[]> vecs, int start, int end)
    {
        int d = vecs[start].Length; //embedding dimension
        var m = new float[d];

        for (int i = start; i < end; i++)
            for (int j = 0; j < d; j++)
                m[j] += vecs[i][j];

        int n = end - start;
        for (int j = 0; j < d; j++) m[j] /= n;

        float norm = (float)Math.Sqrt(m.Sum(x => x * x));
        if (norm > 0)
            for (int j = 0; j < d; j++) m[j] /= norm;

        return m;
    }

    //sliding window approach to find split points based on semantic difference
    public List<int> FindSplitPoints(
        IReadOnlyList<float[]> sentenceEmbeddings,
        int window = 8, //hardcoded value, maybe improve in the future
        float percentile = 0.85f)
    {
        var scores = new List<(int idx, float div)>();
        int half = window / 2;

        for (int i = half; i < sentenceEmbeddings.Count - half; i++)
        {
            var left = Mean(sentenceEmbeddings, i - half, i);
            var right = Mean(sentenceEmbeddings, i, i + half);
            float div = 1f - Dot(left, right);
            scores.Add((i, div));
        }

        if (scores.Count == 0) return new();

        var threshold = scores
            .Select(s => s.div)
            .OrderBy(x => x)
            .ElementAt((int)(scores.Count * percentile));

        return scores
            .Where(s => s.div >= threshold)
            .Select(s => s.idx)
            .OrderBy(i => i)
            .ToList();
    }
}
