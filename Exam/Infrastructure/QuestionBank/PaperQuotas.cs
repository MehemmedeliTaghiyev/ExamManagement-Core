namespace Exam.Infrastructure.Banking
{
    public sealed class PaperQuota
    {
        public int Mcq { get; init; }
        public int OpenCoded { get; init; }
        public int Written { get; init; }
        public int Situation { get; init; }
        public bool FillAny { get; init; }
        public int Total => FillAny ? Mcq : Mcq + OpenCoded + Written + Situation;
    }

    public static class PaperQuotas
    {
        // Rəsmi buraxılış / blok sayları (kind məlum olanda default).
        // Grade 9 buraxılış situation (3) is a later ExamPulse change — from-bank is blok-only.
        public static readonly PaperQuota Grade9Buraxilis = new()
        {
            Mcq = 12,
            OpenCoded = 4,
            Written = 6,
            Situation = 0
        };

        public static readonly PaperQuota Grade11Buraxilis = new()
        {
            Mcq = 13,
            OpenCoded = 5,
            Written = 7,
            Situation = 0
        };

        public static readonly PaperQuota Grade11Blok = new()
        {
            Mcq = 22,
            OpenCoded = 0,
            Written = 5,
            Situation = 3
        };

        public static PaperQuota? ForKnownKind(string? grade, PaperKind kind)
        {
            var g = NormalizeGrade(grade);
            return (g, kind) switch
            {
                ("9", PaperKind.Buraxilis) => Grade9Buraxilis,
                ("11", PaperKind.Buraxilis) => Grade11Buraxilis,
                ("11", PaperKind.Blok) => Grade11Blok,
                _ => null
            };
        }

        public static string NormalizeGrade(string? grade)
        {
            var parsed = BankGrades.Parse(grade);
            return parsed.Count == 0 ? "" : parsed[0];
        }

        /// <summary>Live student weekly/daily papers (ExamPulse). 94=respublika, 96=smoke — not this cohort.</summary>
        public static bool IsStudentWeeklyExam(int id, string? title)
        {
            if (id is 94 or 96) return false;
            if (id is 97 or 98 or 99) return true;
            var t = (title ?? "").Trim().ToLowerInvariant()
                .Replace("ı", "i").Replace("ə", "e").Replace("ö", "o").Replace("ü", "u");
            if (t.Contains("respublika") || t.Contains("buraxil") || t.Contains("specialty"))
                return false;
            return t.Contains("heftelik") || t.Contains("weekly");
        }

        /// <summary>Weekly papers have no exam difficulty (asan/orta). Teacher may still filter questions.</summary>
        public static bool IgnoreExamDifficulty(PaperKind kind, string? audience, bool studentWeeklyPaper = false)
        {
            if (studentWeeklyPaper) return true;
            var a = NormalizeAudience(audience);
            if (a == "teacher") return false;
            if (a == "weekly") return true;
            return kind == PaperKind.Weekly;
        }

        public static PaperKind ParseKind(string? kind, string? audience)
        {
            var k = (kind ?? "").Trim().ToLowerInvariant();
            k = k.Replace("ı", "i").Replace("ə", "e");
            if (k is "blok" or "block" or "specialty") return PaperKind.Blok;
            if (k.Contains("buraxil") || k is "exit") return PaperKind.Buraxilis;
            if (k is "weekly" or "sinaq" or "sınaq") return PaperKind.Weekly;
            if (k is "teacher" or "muellim" or "müəllim") return PaperKind.Teacher;

            var a = (audience ?? "").Trim().ToLowerInvariant();
            if (a is "specialty") return PaperKind.Blok;
            if (a is "weekly") return PaperKind.Weekly;
            if (a is "teacher") return PaperKind.Teacher;
            return PaperKind.Unknown;
        }

        /// <summary>Situation/isbat yalnız specialty (Blok). Buraxılış (o cümlədən 9-cu sinif) yox.</summary>
        public static bool AllowSituation(PaperKind kind, string? audience, string? grade = null)
        {
            _ = grade;
            if (kind is PaperKind.Weekly or PaperKind.Teacher or PaperKind.Buraxilis) return false;
            var a = NormalizeAudience(audience);
            if (a is "weekly" or "teacher") return false;
            if (a == "specialty") return true;
            return kind == PaperKind.Blok && string.IsNullOrWhiteSpace(audience);
        }

        public static bool AllowIsbat(PaperKind kind, string? audience) =>
            AllowSituation(kind, audience);

        public static string? NormalizeAudience(string? audience)
        {
            if (string.IsNullOrWhiteSpace(audience)) return null;
            var a = audience.Trim().ToLowerInvariant()
                .Replace("ı", "i").Replace("ə", "e");
            if (a is "weekly" or "sinaq" or "sinag") return "weekly";
            if (a is "teacher" or "muellim") return "teacher";
            if (a is "specialty" or "blok" or "block") return "specialty";
            return a;
        }

        public static string? AudienceFromKind(PaperKind kind) => kind switch
        {
            PaperKind.Weekly => "weekly",
            PaperKind.Teacher => "teacher",
            PaperKind.Blok => "specialty",
            _ => null
        };

        public static bool BankAudienceFits(string? bankAudience, string requested)
        {
            var bank = NormalizeAudience(bankAudience);
            var need = NormalizeAudience(requested) ?? requested.Trim().ToLowerInvariant();
            if (need is "weekly" or "teacher")
                return bank == need;
            if (need == "specialty")
                return bank is null or "specialty";
            return bank == need;
        }
    }

    public enum PaperKind
    {
        Unknown,
        Weekly,
        Teacher,
        Buraxilis,
        Blok
    }

    public enum BankSlot
    {
        Mcq,
        OpenCoded,
        Written,
        Situation
    }
}
