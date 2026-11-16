namespace Exam.Infrastructure.Banking
{
    /// <summary>
    /// QuestionBank.Grade holds one or more of 9/10/11, comma-separated (e.g. "9,10").
    /// Tokens are whole grades — "10" does not match "1" or "11".
    /// </summary>
    public static class BankGrades
    {
        public static IReadOnlyList<string> Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            var result = new List<string>();
            var current = new char[2];
            var len = 0;
            foreach (var ch in raw)
            {
                if (char.IsDigit(ch))
                {
                    if (len < current.Length)
                        current[len++] = ch;
                    else
                        len = 0;
                }
                else
                {
                    Flush(result, current, ref len);
                }
            }
            Flush(result, current, ref len);
            return result;
        }

        public static string Format(IEnumerable<string> grades)
        {
            var ordered = grades
                .Where(g => g is "9" or "10" or "11")
                .Distinct()
                .OrderBy(Rank)
                .ToList();
            return ordered.Count == 0 ? "" : string.Join(",", ordered);
        }

        public static string Format(string? raw) => Format(Parse(raw));

        public static string Add(string? existing, string grade)
        {
            var list = Parse(existing).ToList();
            if (grade is "9" or "10" or "11" && !list.Contains(grade))
                list.Add(grade);
            return Format(list);
        }

        public static string Union(string? a, string? b) => Format(Parse(a).Concat(Parse(b)));

        public static bool Contains(string? stored, string grade) =>
            Parse(stored).Contains(grade);

        public static bool Overlaps(string? stored, IReadOnlyList<string> wanted)
        {
            if (wanted.Count == 0) return true;
            var have = Parse(stored);
            foreach (var g in wanted)
            {
                if (have.Contains(g)) return true;
            }
            return false;
        }

        public static bool IsBlank(string? stored) => string.IsNullOrWhiteSpace(stored);

        private static int Rank(string g) => g == "9" ? 0 : g == "10" ? 1 : 2;

        private static void Flush(List<string> result, char[] buf, ref int len)
        {
            if (len == 0) return;
            var token = new string(buf, 0, len);
            len = 0;
            if (token is "9" or "10" or "11" && !result.Contains(token))
                result.Add(token);
        }
    }
}
