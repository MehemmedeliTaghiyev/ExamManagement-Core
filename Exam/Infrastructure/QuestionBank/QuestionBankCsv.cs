using System.Globalization;
using System.Text;
using Exam.Core.Domain;
using Exam.Core.Enums;

namespace Exam.Infrastructure.Banking
{
    internal static class QuestionBankCsv
    {
        public static List<QuestionBankImportRow> Parse(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return new List<QuestionBankImportRow>();

            var headers = SplitCsvLine(headerLine)
                .Select((h, i) => (Key: NormalizeHeader(h), Index: i))
                .Where(x => !string.IsNullOrEmpty(x.Key))
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.First().Index, StringComparer.OrdinalIgnoreCase);

            var rows = new List<QuestionBankImportRow>();
            string? line;
            var lineNo = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = SplitCsvLine(line);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, index) in headers)
                {
                    map[key] = index < cols.Count ? cols[index].Trim() : "";
                }
                rows.Add(MapRow(map, lineNo));
            }

            return rows;
        }

        private static QuestionBankImportRow MapRow(IReadOnlyDictionary<string, string> map, int lineNo)
        {
            var qnum = First(map, "qnum", "q_num", "number");
            var text = First(map, "text", "question", "stemtext");
            var stem = First(map, "stem");
            if (!string.IsNullOrWhiteSpace(stem) && !text.Contains(stem, StringComparison.Ordinal))
                text = string.IsNullOrWhiteSpace(text) ? stem : stem + "\n\n" + text;

            var typeRaw = First(map, "type");
            var inputKind = First(map, "inputkind", "input_kind");
            var correct = First(map, "correcttext", "correct_text", "correct_answer", "answer");
            var difficulty = First(map, "difficultylevel", "difficulty_level", "difficulty");
            var pointsRaw = First(map, "points");
            var grade = First(map, "grade", "target_grade", "targetgrade");
            var subject = First(map, "subject");
            var audience = First(map, "audience");
            var topic = First(map, "maintopic", "main_topic", "topic");
            var language = First(map, "language", "lang");
            var situationId = EmptyToNull(First(map, "situationid", "situation_id"));
            var situationTitle = EmptyToNull(First(map, "situationtitle", "situation_title"));
            var figure = EmptyToNull(First(map, "figureurl", "figure_url", "figure_path", "figure", "imageurl"));

            var type = ParseType(typeRaw, inputKind, map);
            inputKind = NormalizeInputKind(inputKind, type);
            var points = ParsePoints(pointsRaw, difficulty);

            var options = ParseOptions(map, correct);

            return new QuestionBankImportRow
            {
                Line = lineNo,
                Entity = new QuestionBank
                {
                    Qnum = qnum.Trim(),
                    Text = text,
                    Type = type,
                    InputKind = inputKind,
                    CorrectText = EmptyToNull(correct),
                    DifficultyLevel = EmptyToNull(difficulty),
                    Points = points,
                    Grade = Trunc(grade, 8),
                    Subject = Trunc(subject, 32),
                    Audience = NormalizeAudience(audience),
                    MainTopic = Trunc(topic, 200),
                    Language = Trunc(NormalizeLanguage(language), 8),
                    SituationId = Trunc(situationId, 64),
                    SituationTitle = Trunc(situationTitle, 300),
                    FigureUrl = Trunc(figure, 1000)
                },
                Options = options
            };
        }

        private static List<QuestionBankOption> ParseOptions(IReadOnlyDictionary<string, string> map, string correct)
        {
            var options = new List<QuestionBankOption>();
            var json = First(map, "options");
            if (!string.IsNullOrWhiteSpace(json) && (json.StartsWith("[") || json.StartsWith("{")))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var i = 0;
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            string text;
                            var isCorrect = false;
                            if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                text = el.GetString() ?? "";
                            }
                            else
                            {
                                text = el.TryGetProperty("optionText", out var ot) ? ot.GetString() ?? ""
                                    : el.TryGetProperty("text", out var t) ? t.GetString() ?? ""
                                    : el.ToString();
                                isCorrect = el.TryGetProperty("isCorrect", out var ic) && ic.ValueKind is System.Text.Json.JsonValueKind.True;
                            }
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                options.Add(new QuestionBankOption
                                {
                                    OptionText = text,
                                    IsCorrect = isCorrect || MatchesCorrect(text, correct, i)
                                });
                            }
                            i++;
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // fall through to letter columns
                }
            }

            if (options.Count == 0)
            {
                var letters = new[] { "A", "B", "C", "D", "E" };
                var aliases = new[] { "optiona", "option_a", "option1", "option_1" };
                for (var i = 0; i < letters.Length; i++)
                {
                    var text = First(map,
                        "option" + letters[i].ToLowerInvariant(),
                        "option_" + letters[i].ToLowerInvariant(),
                        "option" + (i + 1),
                        "option_" + (i + 1),
                        letters[i].ToLowerInvariant());
                    _ = aliases;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    options.Add(new QuestionBankOption
                    {
                        OptionText = text,
                        IsCorrect = MatchesCorrect(text, correct, i)
                    });
                }
            }

            return options;
        }

        private static bool MatchesCorrect(string optionText, string correct, int index)
        {
            if (string.IsNullOrWhiteSpace(correct)) return false;
            var c = correct.Trim();
            if (c.Equals(optionText.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            if (c.Length == 1)
            {
                var ch = char.ToUpperInvariant(c[0]);
                if (ch is >= 'A' and <= 'E' && ch - 'A' == index) return true;
            }
            return false;
        }

        private static QuestionType ParseType(string typeRaw, string inputKind, IReadOnlyDictionary<string, string> map)
        {
            if (Enum.TryParse<QuestionType>(typeRaw, true, out var parsed))
                return parsed;
            var t = typeRaw.Trim().ToLowerInvariant();
            if (t is "open" or "openended" or "open_ended" or "yazili" or "yazılı" or "text")
                return QuestionType.OpenEnded;
            if (t is "mcq" or "closed" or "single" or "choice")
                return QuestionType.SingleChoice;
            if (!string.IsNullOrWhiteSpace(First(map, "options")) || !string.IsNullOrWhiteSpace(First(map, "optiona", "option_a", "a")))
                return QuestionType.SingleChoice;
            if (IsOpenKind(inputKind))
                return QuestionType.OpenEnded;
            return QuestionType.SingleChoice;
        }

        private static string NormalizeInputKind(string inputKind, QuestionType type)
        {
            if (string.IsNullOrWhiteSpace(inputKind))
                return type == QuestionType.OpenEnded ? "Text" : "Choice";
            var k = inputKind.Trim();
            if (k.Equals("Choice", StringComparison.OrdinalIgnoreCase)) return "Choice";
            if (k.Equals("Text", StringComparison.OrdinalIgnoreCase)) return "Text";
            if (k.Equals("Integer", StringComparison.OrdinalIgnoreCase)) return "Integer";
            if (k.Equals("Decimal", StringComparison.OrdinalIgnoreCase)) return "Decimal";
            if (k.Equals("Number", StringComparison.OrdinalIgnoreCase)) return "Number";
            if (k.Equals("coded", StringComparison.OrdinalIgnoreCase) || k.Equals("open_coded", StringComparison.OrdinalIgnoreCase))
                return "Integer";
            return type == QuestionType.OpenEnded ? "Text" : "Choice";
        }

        internal static bool IsOpenKind(string? kind)
        {
            if (string.IsNullOrWhiteSpace(kind)) return false;
            return kind.Equals("Text", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Integer", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Decimal", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Number", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParsePoints(string raw, string difficulty)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                return n;
            var d = (difficulty ?? "").Trim().ToLowerInvariant()
                .Replace("ə", "e").Replace("ı", "i").Replace("ç", "c");
            if (d is "sade" or "easy" or "asan" or "simple") return 1;
            if (d is "orta" or "medium" or "middle") return 2;
            if (d is "cetin" or "hard" or "cetinlik") return 3;
            if (d.Contains("cox") || d is "very hard" or "cox cetin") return 4;
            return 1;
        }

        private static string? NormalizeAudience(string audience)
        {
            if (string.IsNullOrWhiteSpace(audience)) return null;
            var a = audience.Trim().ToLowerInvariant();
            if (a is "weekly" or "teacher" or "specialty") return a;
            if (a is "sinaq" or "sınaq") return "weekly";
            if (a is "blok" or "block") return "specialty";
            return Trunc(a, 20);
        }

        private static string NormalizeLanguage(string language)
        {
            var l = (language ?? "").Trim().ToLowerInvariant();
            if (l is "azərbaycan" or "azerbaycan" or "az") return "az";
            if (l is "rus" or "ru" or "russian") return "ru";
            if (l is "ingilis" or "en" or "english") return "en";
            return l;
        }

        private static string First(IReadOnlyDictionary<string, string> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return "";
        }

        private static string? EmptyToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Trunc(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var v = value.Trim();
            return v.Length <= max ? v : v[..max];
        }

        private static string NormalizeHeader(string header)
        {
            var sb = new StringBuilder();
            foreach (var ch in header.Trim())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        internal static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
            result.Add(sb.ToString());
            return result;
        }
    }

    internal sealed class QuestionBankImportRow
    {
        public int Line { get; init; }
        public QuestionBank Entity { get; init; } = null!;
        public List<QuestionBankOption> Options { get; init; } = new();
    }
}
