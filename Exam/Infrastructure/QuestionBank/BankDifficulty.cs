using Exam.Core.Domain;

namespace Exam.Infrastructure.Banking
{
    /// <summary>
    /// Downward difficulty fallback when a topic quota has no remaining rows:
    /// çətin → orta → asan; orta → asan; asan stays asan. C# only.
    /// </summary>
    public static class BankDifficulty
    {
        public const string Asan = "asan";
        public const string Orta = "orta";
        public const string Cetin = "cetin";

        public static readonly string[] HardestFirst = { Cetin, Orta, Asan };

        public static string Band(QuestionBank q) => Band(q.DifficultyLevel);

        public static string Band(string? raw)
        {
            var d = (raw ?? "").Trim().ToLowerInvariant()
                .Replace("ə", "e").Replace("ı", "i").Replace("ç", "c");
            if (d is "sade" or "easy" or "asan" or "simple") return Asan;
            if (d is "orta" or "medium" or "middle") return Orta;
            if (d is "cetin" or "hard" or "cetinlik" || d.Contains("cetin") || d is "very hard")
                return Cetin;
            return d.Length == 0 ? "unknown" : d;
        }

        public static IReadOnlyList<string> Chain(string band) => band switch
        {
            Cetin => new[] { Cetin, Orta, Asan },
            Orta => new[] { Orta, Asan },
            Asan => new[] { Asan },
            _ => new[] { band, Orta, Asan }
        };

        public static Dictionary<string, int>? Plan(
            int need,
            bool mix,
            int? asan,
            int? orta,
            int? cetin,
            string? difficultyLevel)
        {
            if (need <= 0) return null;
            var a = Math.Max(0, asan ?? 0);
            var o = Math.Max(0, orta ?? 0);
            var c = Math.Max(0, cetin ?? 0);
            if (a + o + c > 0)
                return Scale(new Dictionary<string, int> { [Asan] = a, [Orta] = o, [Cetin] = c }, need);

            var listed = ParseLevels(difficultyLevel);
            if (mix)
                return Mix305020(need);
            if (listed.Count == 1)
                return new Dictionary<string, int> { [listed[0]] = need };
            if (listed.Count > 1)
                return KeepListed(Mix305020(need), listed, need);
            return null;
        }

        public static List<QuestionBank> Fill(
            IEnumerable<QuestionBank> candidates,
            int need,
            IReadOnlyList<QuestionBank> already,
            IReadOnlyDictionary<string, int> copies,
            Random rng,
            IReadOnlyDictionary<string, int>? bands,
            out bool usedFallback,
            bool strict = false,
            IReadOnlySet<string>? usedQnums = null)
        {
            usedFallback = false;
            if (need <= 0) return new List<QuestionBank>();
            var pool = candidates.ToList();
            if (pool.Count == 0) return new List<QuestionBank>();
            if (bands == null || bands.Values.Sum() <= 0)
                return BankUsage.TakePreferred(pool, need, already, copies, rng, usedQnums);

            var chosen = already.ToList();
            var chosenIds = chosen.Select(q => q.Id).ToHashSet();
            var result = new List<QuestionBank>();
            var groups = pool
                .GroupBy(TopicKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var shares = LargestRemainder(
                groups.ToDictionary(kv => kv.Key, kv => (double)kv.Value.Count, StringComparer.OrdinalIgnoreCase),
                need);

            foreach (var topic in shares.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                var n = shares[topic];
                if (n <= 0) continue;
                var topicBands = LargestRemainder(
                    bands.ToDictionary(kv => kv.Key, kv => (double)Math.Max(0, kv.Value), StringComparer.OrdinalIgnoreCase),
                    n);
                var topicPool = groups[topic];
                var takenHere = 0;
                foreach (var band in BandOrder(topicBands.Keys))
                {
                    var want = topicBands.GetValueOrDefault(band);
                    if (want <= 0) continue;
                    var chain = strict ? new[] { band } : Chain(band);
                    var got = TakeChain(topicPool, want, result, chosen, chosenIds, copies, rng, chain, usedQnums, out var fell);
                    takenHere += got;
                    if (fell) usedFallback = true;
                    if (result.Count >= need) return result;
                }

                var still = n - takenHere;
                if (still > 0)
                {
                    var rest = topicPool.Where(q => !chosenIds.Contains(q.Id));
                    var extra = BankUsage.TakePreferred(rest, still, chosen, copies, rng, usedQnums);
                    AddTaken(extra, result, chosen, chosenIds);
                    if (result.Count >= need) return result;
                }
            }

            if (result.Count < need)
            {
                var rest = pool.Where(q => !chosenIds.Contains(q.Id));
                var extra = BankUsage.TakePreferred(rest, need - result.Count, chosen, copies, rng, usedQnums);
                AddTaken(extra, result, chosen, chosenIds);
            }

            return result;
        }

        private static int TakeChain(
            List<QuestionBank> topicPool,
            int want,
            List<QuestionBank> result,
            List<QuestionBank> chosen,
            HashSet<int> chosenIds,
            IReadOnlyDictionary<string, int> copies,
            Random rng,
            IReadOnlyList<string> chain,
            IReadOnlySet<string>? usedQnums,
            out bool usedFallback)
        {
            usedFallback = false;
            var got = 0;
            var preferred = chain[0];
            foreach (var band in chain)
            {
                if (got >= want) break;
                var slice = topicPool.Where(q => !chosenIds.Contains(q.Id) && Band(q) == band);
                var take = BankUsage.TakePreferred(slice, want - got, chosen, copies, rng, usedQnums);
                if (take.Count == 0) continue;
                if (band != preferred) usedFallback = true;
                AddTaken(take, result, chosen, chosenIds);
                got += take.Count;
            }
            return got;
        }

        private static void AddTaken(
            List<QuestionBank> take,
            List<QuestionBank> result,
            List<QuestionBank> chosen,
            HashSet<int> chosenIds)
        {
            foreach (var q in take)
            {
                if (!chosenIds.Add(q.Id)) continue;
                result.Add(q);
                chosen.Add(q);
            }
        }

        private static string TopicKey(QuestionBank q) =>
            string.IsNullOrWhiteSpace(q.MainTopic) ? "" : q.MainTopic.Trim();

        private static List<string> ParseLevels(string? difficultyLevel)
        {
            if (string.IsNullOrWhiteSpace(difficultyLevel)) return new List<string>();
            return difficultyLevel
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Band)
                .Where(b => b is Asan or Orta or Cetin)
                .Distinct()
                .ToList();
        }

        private static Dictionary<string, int> Mix305020(int need) =>
            LargestRemainder(new Dictionary<string, double>
            {
                [Asan] = 30,
                [Orta] = 50,
                [Cetin] = 20
            }, need);

        private static Dictionary<string, int> Scale(Dictionary<string, int> counts, int need) =>
            LargestRemainder(
                counts.ToDictionary(kv => kv.Key, kv => (double)Math.Max(0, kv.Value), StringComparer.OrdinalIgnoreCase),
                need);

        private static Dictionary<string, int> KeepListed(
            Dictionary<string, int> mix,
            List<string> listed,
            int need)
        {
            var weights = listed.ToDictionary(
                b => b,
                b => (double)mix.GetValueOrDefault(b, 1),
                StringComparer.OrdinalIgnoreCase);
            return LargestRemainder(weights, need);
        }

        private static IEnumerable<string> BandOrder(IEnumerable<string> keys)
        {
            var set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            foreach (var b in HardestFirst)
            {
                if (set.Remove(b)) yield return b;
            }
            foreach (var b in set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                yield return b;
        }

        internal static Dictionary<string, int> LargestRemainder(Dictionary<string, double> weights, int total)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (total <= 0 || weights.Count == 0) return result;
            var active = weights.Where(kv => kv.Value > 0).ToList();
            if (active.Count == 0) return result;
            var sum = active.Sum(kv => kv.Value);
            if (sum <= 0) return result;
            var raw = active.ToDictionary(kv => kv.Key, kv => kv.Value / sum * total, StringComparer.OrdinalIgnoreCase);
            var given = 0;
            foreach (var kv in raw)
            {
                var n = (int)Math.Floor(kv.Value);
                result[kv.Key] = n;
                given += n;
            }
            var left = total - given;
            foreach (var k in raw.OrderByDescending(kv => kv.Value - result[kv.Key]).Select(kv => kv.Key).Take(left))
                result[k] = result.GetValueOrDefault(k) + 1;
            return result;
        }
    }
}
