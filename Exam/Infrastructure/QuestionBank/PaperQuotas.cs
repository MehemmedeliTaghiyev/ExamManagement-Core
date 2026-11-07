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
        public static readonly PaperQuota Grade9Buraxilis = new()
        {
            Mcq = 12,
            OpenCoded = 4,
            Written = 6,
            Situation = 3
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
            if (string.IsNullOrWhiteSpace(grade)) return "";
            var digits = new string(grade.Where(char.IsDigit).ToArray());
            return digits;
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

        public static bool AllowSituation(PaperKind kind, string? audience, string? grade)
        {
            if (kind is PaperKind.Weekly or PaperKind.Teacher) return false;
            var a = (audience ?? "").Trim().ToLowerInvariant();
            if (a is "weekly" or "teacher") return false;
            if (kind == PaperKind.Blok || a == "specialty") return true;
            // Grade 9 buraxılış rəsmi kağızında 3 situasiya var.
            if (kind == PaperKind.Buraxilis && NormalizeGrade(grade) == "9") return true;
            return false;
        }

        public static bool AllowIsbat(PaperKind kind, string? audience)
        {
            if (kind is PaperKind.Weekly or PaperKind.Teacher) return false;
            var a = (audience ?? "").Trim().ToLowerInvariant();
            if (a is "weekly" or "teacher") return false;
            return kind == PaperKind.Blok || a == "specialty";
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
