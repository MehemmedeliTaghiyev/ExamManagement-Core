using System.Security.Cryptography;
using System.Text;
using Exam.Core.Domain;

namespace Exam.Infrastructure.Banking
{
    /// <summary>
    /// In-quiz near-clone check (hashed character n-grams). Same 0.88 cosine as the
    /// pipeline; no Python on the from-bank path.
    /// </summary>
    public static class BankSimilarity
    {
        public const double Threshold = 0.88;
        public const int Dim = 384;

        public static bool TooClose(QuestionBank candidate, IReadOnlyList<QuestionBank> chosen)
        {
            if (chosen.Count == 0) return false;
            var emb = Embed(Blob(candidate));
            foreach (var other in chosen)
            {
                if (Cosine(emb, Embed(Blob(other))) >= Threshold)
                    return true;
            }
            return false;
        }

        internal static string Blob(QuestionBank q) =>
            $"{q.SituationTitle}\n{q.Text}";

        internal static double[] Embed(string text)
        {
            var vec = new double[Dim];
            var blob = Normalize(text);
            for (var n = 2; n <= 4; n++)
            {
                if (blob.Length < n) continue;
                for (var i = 0; i <= blob.Length - n; i++)
                    vec[Bin(blob.Substring(i, n))] += 1.0;
            }

            var i0 = 0;
            while (i0 < blob.Length)
            {
                if (!char.IsLetterOrDigit(blob[i0])) { i0++; continue; }
                var i1 = i0 + 1;
                while (i1 < blob.Length && char.IsLetterOrDigit(blob[i1])) i1++;
                vec[Bin(blob.Substring(i0, i1 - i0))] += 2.0;
                i0 = i1;
            }

            var norm = Math.Sqrt(vec.Sum(x => x * x));
            if (norm <= 0) return vec;
            for (var i = 0; i < vec.Length; i++)
                vec[i] /= norm;
            return vec;
        }

        internal static double Cosine(double[] a, double[] b)
        {
            if (a.Length != b.Length) return 0;
            var s = 0.0;
            for (var i = 0; i < a.Length; i++)
                s += a[i] * b[i];
            return s;
        }

        private static string Normalize(string text)
        {
            var sb = new StringBuilder((text ?? "").Length);
            foreach (var ch in (text ?? "").ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private static int Bin(string token)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(token));
            var idx = hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24);
            return (int)((uint)idx % Dim);
        }
    }
}
