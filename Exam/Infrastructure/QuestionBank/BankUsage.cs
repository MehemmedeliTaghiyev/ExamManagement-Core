using Exam.Core.Domain;

namespace Exam.Infrastructure.Banking
{
    /// <summary>
    /// From-bank prefers never-copied SourceQnum, then lowest UseCount, then oldest LastUsedAt.
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

        public static bool Unused(QuestionBank q, IReadOnlyDictionary<string, int> copies)
            => EffectiveUse(q, copies) <= 0;

        public static List<QuestionBank> OrderByPreference(
            IEnumerable<QuestionBank> candidates,
            IReadOnlyDictionary<string, int> copies,
            Random rng)
        {
            return candidates
                .OrderBy(q => Unused(q, copies) ? 0 : 1)
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
            Random rng)
        {
            if (need <= 0) return new List<QuestionBank>();
            var usedIds = new HashSet<int>(already.Select(q => q.Id));
            return OrderByPreference(candidates.Where(q => !usedIds.Contains(q.Id)), copies, rng)
                .Take(need)
                .ToList();
        }
    }
}
