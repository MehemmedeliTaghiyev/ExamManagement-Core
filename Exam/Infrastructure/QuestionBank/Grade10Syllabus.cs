namespace Exam.Infrastructure.Banking
{
    /// <summary>
    /// ExamPulse 10th-grade mathematics syllabus (DİM program).
    /// Bank MainTopic uses these chapter names. Frontend StudentSyllabus generic
    /// Riyaziyyat labels that overlap: Natural ədədlər və bölünmə, Kəsrlər və faiz,
    /// Tənliklər, Bərabərsizliklər, Funksiyalar, Triqonometriya, Planimetriya,
    /// Stereometriya (prisms/pyramids, not fırlanma), Ehtimal və statistika.
    /// Not grade 10: Ardıcıllıqlar və silsilə, Törəmə, İntegral.
    /// Not taken from the grade-11 February send calendar.
    /// </summary>
    public static class Grade10Syllabus
    {
        public static readonly IReadOnlyList<string> Topics = new[]
        {
            "Həqiqi ədədlər",
            "Kompleks ədədlər",
            "Nisbət. Tənasüb. Faiz",
            "Rasional ifadələr",
            "n-ci dərəcədən kök. İrrasional ifadələr. Üstlü qüvvət",
            "Ölçmə. Statistika. Çoxluqlar. Birləşmələr və ehtimal",
            "Birdəyişənli tənliklər və məsələ həlli",
            "Tənliklər sistemi və məsələ həlli",
            "Bərabərsizliklər",
            "Funksiyalar və qrafiklər",
            "Triqonometrik funksiyalar. Triqonometrik tənliklər",
            "Üstlü və loqarifmik funksiyalar",
            "Üstlü və loqarifmik tənliklər və bərabərsizliklər",
            "Sadə həndəsi fiqurlar",
            "Çevrə və dairə",
            "Üçbucaqlar",
            "Dördbucaqlılar və çoxbucaqlılar",
            "Fiqurların sahəsi",
            "Fəzada nöqtə, düz xətt, müstəvi",
            "Çoxüzlülər, onların səthi və həcmi"
        };

        /// <summary>Grade-11-only (or Blok-only) chapters — never tagged grade 10.</summary>
        public static readonly IReadOnlyList<string> ExcludedTopics = new[]
        {
            "Ədədi ardıcıllıqlar. Silsilələr",
            "Koordinatlar üsulu. Hərəkət. Oxşarlıq. Vektorlar",
            "Fırlanma fiqurları, onların səthi və həcmi",
            "Törəmə və onun tətbiqləri",
            "Ardıcıllığın və funksiyanın limiti",
            "İbtidai funksiya və inteqral",
            "Proqramdan kənar mövzular"
        };

        private static readonly HashSet<string> TopicSet = new(Topics, StringComparer.Ordinal);

        public static bool Matches(string? mainTopic)
        {
            if (string.IsNullOrWhiteSpace(mainTopic)) return false;
            return TopicSet.Contains(mainTopic.Trim());
        }

        /// <summary>
        /// Keep 9/11 and add 10 when the bank chapter is on the grade-10 syllabus.
        /// Empty Grade is left empty (not a 9/11 row).
        /// </summary>
        public static string? Tag(string? grade, string? mainTopic)
        {
            var formatted = BankGrades.Format(grade);
            if (!Matches(mainTopic))
                return string.IsNullOrEmpty(formatted) ? grade?.Trim() : formatted;

            var parsed = BankGrades.Parse(grade);
            if (parsed.Count == 0)
                return string.IsNullOrEmpty(formatted) ? grade?.Trim() : formatted;

            if (parsed.Contains("9") || parsed.Contains("11") || parsed.Contains("10"))
            {
                var tagged = BankGrades.Add(grade, "10");
                return string.IsNullOrEmpty(tagged) ? grade : tagged;
            }

            return string.IsNullOrEmpty(formatted) ? grade : formatted;
        }
    }
}
