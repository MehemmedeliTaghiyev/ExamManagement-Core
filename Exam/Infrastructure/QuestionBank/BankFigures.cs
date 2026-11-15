using System.Text.RegularExpressions;

namespace Exam.Infrastructure.Banking
{
    /// <summary>
    /// Figured bank items are stem (şərt) + figure URL(s). uniqueness_text stays in local
    /// SQLite for cosine; it is not a second student-facing QuestionBank row or column.
    /// </summary>
    public static class BankFigures
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        public static IReadOnlyList<string> Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            var parts = raw.Split(new[] { ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            foreach (var part in parts)
            {
                var url = part.Trim();
                if (url.Length == 0) continue;
                if (!result.Contains(url, StringComparer.OrdinalIgnoreCase))
                    result.Add(url);
            }
            return result;
        }

        public static string? Join(IEnumerable<string> urls)
        {
            var list = urls
                .Select(u => (u ?? "").Trim())
                .Where(u => u.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return list.Count == 0 ? null : string.Join(";", list);
        }

        public static bool HasFigure(string? figureRaw) => Parse(figureRaw).Count > 0;

        public static string? First(string? figureRaw)
        {
            var list = Parse(figureRaw);
            return list.Count == 0 ? null : list[0];
        }

        /// <summary>
        /// Student-facing bank text. When figures exist, keep the stem only — do not
        /// concatenate uniqueness/OCR dumps of the drawing.
        /// </summary>
        public static string StudentFacingText(string? stem, string? body, string? figureRaw)
        {
            var s = (stem ?? "").Trim();
            var t = (body ?? "").Trim();
            if (HasFigure(figureRaw))
                return s.Length > 0 ? s : t;
            if (s.Length > 0 && t.Length > 0 && !t.Contains(s, StringComparison.Ordinal))
                return s + "\n\n" + t;
            return t.Length > 0 ? t : s;
        }

        public static string StemKey(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return Whitespace.Replace(text.Trim(), " ");
        }

        /// <summary>
        /// True when <paramref name="candidate"/> is a text-only copy of a figured item
        /// (same stem/body, no figure of its own).
        /// </summary>
        public static bool IsTextOnlyDuplicate(string? candidateText, string? candidateFigure, string? candidateTopic,
            string? figuredText, string? figuredFigure, string? figuredTopic)
        {
            if (HasFigure(candidateFigure)) return false;
            if (!HasFigure(figuredFigure)) return false;
            var a = StemKey(candidateText);
            var b = StemKey(figuredText);
            if (a.Length < 24 || b.Length < 24) return false;
            if (!(a == b || a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)))
                return false;
            if (string.IsNullOrWhiteSpace(candidateTopic) || string.IsNullOrWhiteSpace(figuredTopic))
                return true;
            return string.Equals(candidateTopic.Trim(), figuredTopic.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
