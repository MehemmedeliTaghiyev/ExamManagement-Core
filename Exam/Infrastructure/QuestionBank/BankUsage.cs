using Exam.Core.Domain;

namespace Exam.Infrastructure.Banking
{
    /// <summary>
    /// Teacher from-bank prefers never-copied SourceQnum. Student weekly/daily papers
    /// skip Qnums already on prior weekly exams for that cohort, then lowest use.
    /// Reuse least-used only when the unused pool cannot fill the quota.
    /// </summary>
    public static class BankUsage
    {
        public static int EffectiveUse(QuestionBank q, IReadOnlyDictionary<string, int> copies)
        {
            var fromExams = 0;
            if (!string.IsNullOrWhiteSpace(q.Qnum))
                copies.TryGetValue(q.Qnum, out fromExams);
            return Math.Max(q.UseCount, fromExams);
        }

        public static bool Unused(
            QuestionBank q,
            IReadOnlyDictionary<string, int> copies,
            IReadOnlySet<string>? usedQnums = null)
        {
            if (usedQnums is { Count: > 0 } && !string.IsNullOrWhiteSpace(q.Qnum) && usedQnums.Contains(q.Qnum))
                return false;
            if (usedQnums is { Count: > 0 })
                return true;
            return EffectiveUse(q, copies) <= 0;
        }

        public static List<QuestionBank> OrderByPreference(
            IEnumerable<QuestionBank> candidates,
            IReadOnlyDictionary<string, int> copies,
            Random rng,
            IReadOnlySet<string>? usedQnums = null)
        {
            return candidates
                .OrderBy(q => Unused(q, copies, usedQnums) ? 0 : 1)
                .ThenBy(q => EffectiveUse(q, copies))
                .ThenBy(q => q.LastUsedAt ?? DateTime.MinValue)
                .ThenBy(_ => rng.Next())
                .ToList();
        }

        public static List<QuestionBank> TakePreferred(
            IEnumerable<QuestionBank> candidates,
            int need,
            IReadOnlyList<QuestionBank> already,
            IReadOnlyDictionary<string, int> copies,
            Random rng,
            IReadOnlySet<string>? usedQnums = null)
        {
            if (need <= 0) return new List<QuestionBank>();
            var chosen = already.ToList();
            var result = new List<QuestionBank>();
            foreach (var q in OrderByPreference(candidates, copies, rng, usedQnums))
            {
                if (result.Count >= need) break;
                if (BankSimilarity.TooClose(q, chosen)) continue;
                result.Add(q);
                chosen.Add(q);
            }
            return result;
        }
    }
}
