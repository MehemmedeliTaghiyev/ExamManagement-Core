using Exam.Core.Domain;
using Exam.Core.DTOs.Exam;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Services
{
    public class QuestionService : IQuestionService
    {
        private static readonly string[] ChoiceLetters = { "A", "B", "C", "D", "Digər" };

        private readonly ExamDbContext _context;

        public QuestionService(ExamDbContext context)
        {
            _context = context;
        }

        public async Task<List<QuestionResponseDto>> GetQuestionsByExamIdAsync(int examId)
        {
            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            var questionIds = questions.Select(q => q.Id).ToList();
            var options = await _context.QuestionOptions
                .AsNoTracking()
                .Where(o => questionIds.Contains(o.QuestionId))
                .ToListAsync();

            return questions.Select(q => new QuestionResponseDto
            {
                Id = q.Id,
                ExamId = q.ExamId,
                Text = q.Text,
                Points = q.Points,
                Type = q.Type,
                InputKind = string.IsNullOrWhiteSpace(q.InputKind)
                    ? (q.Type == QuestionType.OpenEnded ? "Text" : "Choice")
                    : q.InputKind,
                CorrectText = q.CorrectText,
                Options = options
                    .Where(o => o.QuestionId == q.Id)
                    .Select(o => new QuestionOptionResponseDto
                    {
                        Id = o.Id,
                        QuestionId = o.QuestionId,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    })
                    .ToList()
            }).ToList();
        }

        public async Task<Question> CreateQuestionAsync(int examId, CreateQuestionDto dto)
        {
            var question = new Question
            {
                ExamId = examId,
                Text = dto.Text,
                Points = dto.Points,
                Type = dto.Type,
                InputKind = string.IsNullOrWhiteSpace(dto.InputKind)
                    ? (dto.Type == QuestionType.OpenEnded ? "Text" : "Choice")
                    : dto.InputKind,
                CorrectText = dto.CorrectText
            };

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();

            if (dto.Options != null && dto.Options.Any() && question.Type != QuestionType.OpenEnded)
            {
                var options = dto.Options.Select(o => new QuestionOption
                {
                    QuestionId = question.Id,
                    OptionText = string.IsNullOrWhiteSpace(o.OptionText) ? (o.Text ?? string.Empty) : o.OptionText,
                    IsCorrect = o.IsCorrect
                }).ToList();

                if (!options.Any(IsOtherOption))
                {
                    options.Add(new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = "Digər",
                        IsCorrect = false
                    });
                }

                _context.QuestionOptions.AddRange(options);
                await _context.SaveChangesAsync();
            }

            return question;
        }

        public async Task<int> EnsureChoiceSlotsAsync(int examId, int count)
        {
            count = Math.Clamp(count, 1, 200);
            var existing = await _context.Questions
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            var existingIds = existing.Select(q => q.Id).ToList();
            var existingOptions = existingIds.Count == 0
                ? new List<QuestionOption>()
                : await _context.QuestionOptions
                    .Where(o => existingIds.Contains(o.QuestionId))
                    .ToListAsync();

            foreach (var question in existing.Where(q => q.Type != QuestionType.OpenEnded))
            {
                var opts = existingOptions.Where(o => o.QuestionId == question.Id).ToList();
                if (!opts.Any(IsOtherOption))
                {
                    _context.QuestionOptions.Add(new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = "Digər",
                        IsCorrect = false
                    });
                }
            }

            var created = 0;
            for (var i = existing.Count + 1; i <= count; i++)
            {
                var question = new Question
                {
                    ExamId = examId,
                    Text = $"Sual {i}",
                    Points = 1,
                    Type = QuestionType.SingleChoice,
                    InputKind = "Choice"
                };
                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                _context.QuestionOptions.AddRange(ChoiceLetters.Select(letter =>
                    new QuestionOption { QuestionId = question.Id, OptionText = letter, IsCorrect = false }));
                await _context.SaveChangesAsync();
                created++;
            }

            var exam = await _context.Exams.FindAsync(examId);
            if (exam != null)
            {
                var total = await _context.Questions.CountAsync(q => q.ExamId == examId);
                exam.TotalQuestions = total;
                await _context.SaveChangesAsync();
            }

            return created;
        }

        public async Task SetCorrectLettersAsync(int examId, IReadOnlyList<AnswerKeyItemDto> items)
        {
            var questions = await _context.Questions
                .Where(q => q.ExamId == examId)
                .ToListAsync();
            var questionIds = questions.Select(q => q.Id).ToList();

            var options = await _context.QuestionOptions
                .Where(o => questionIds.Contains(o.QuestionId))
                .ToListAsync();

            foreach (var item in items)
            {
                var question = questions.FirstOrDefault(q => q.Id == item.QuestionId);
                if (question == null) continue;

                if (!string.IsNullOrWhiteSpace(item.Type)
                    && Enum.TryParse<QuestionType>(item.Type, true, out var parsedType))
                {
                    question.Type = parsedType;
                }

                if (!string.IsNullOrWhiteSpace(item.InputKind))
                {
                    question.InputKind = item.InputKind;
                }

                if (question.Type == QuestionType.OpenEnded
                    || IsOpenKind(question.InputKind)
                    || !string.IsNullOrWhiteSpace(item.CorrectText))
                {
                    question.CorrectText = item.CorrectText?.Trim();
                    if (question.Type != QuestionType.OpenEnded && IsOpenKind(item.InputKind))
                    {
                        question.Type = QuestionType.OpenEnded;
                    }
                    continue;
                }

                var qOpts = options.Where(o => o.QuestionId == item.QuestionId).OrderBy(o => o.Id).ToList();
                var idx = LetterToIndex(item.CorrectLetter, qOpts);
                if (idx < 0 || idx >= qOpts.Count) continue;
                for (var i = 0; i < qOpts.Count; i++)
                {
                    qOpts[i].IsCorrect = i == idx;
                }
            }

            await _context.SaveChangesAsync();
        }

        private static bool IsOpenKind(string? kind)
        {
            if (string.IsNullOrWhiteSpace(kind)) return false;
            return kind.Equals("Text", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Integer", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Decimal", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Number", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOtherOption(QuestionOption option)
        {
            var text = (option.OptionText ?? "").Trim();
            return text.Equals("Digər", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Diger", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Other", StringComparison.OrdinalIgnoreCase)
                || text.Equals("E", StringComparison.OrdinalIgnoreCase);
        }

        private static int LetterToIndex(string? letter, List<QuestionOption> options)
        {
            if (string.IsNullOrWhiteSpace(letter)) return -1;
            var raw = letter.Trim();
            if (raw.Equals("Digər", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("Diger", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("Other", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                var other = options.FindIndex(IsOtherOption);
                return other >= 0 ? other : Math.Min(4, options.Count - 1);
            }

            var ch = char.ToUpperInvariant(raw[0]);
            return ch is >= 'A' and <= 'E' ? ch - 'A' : -1;
        }

        public async Task<List<QuestionDifficultyDto>> GetQuestionDifficultyAsync(int examId)
        {
            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            var sessionIds = await _context.StudentExams
                .Where(se => se.ExamId == examId && se.Status != StudentExamStatus.InProgress)
                .Select(se => se.Id)
                .ToListAsync();

            var participants = sessionIds.Count;
            var questionIds = questions.Select(q => q.Id).ToList();
            var options = questionIds.Count == 0
                ? new List<QuestionOption>()
                : await _context.QuestionOptions
                    .AsNoTracking()
                    .Where(o => questionIds.Contains(o.QuestionId))
                    .ToListAsync();

            var answers = sessionIds.Count == 0
                ? new List<StudentAnswer>()
                : await _context.StudentAnswers
                    .AsNoTracking()
                    .Where(a => sessionIds.Contains(a.StudentExamId))
                    .ToListAsync();

            return questions.Select((q, idx) =>
            {
                var correct = options.FirstOrDefault(o => o.QuestionId == q.Id && o.IsCorrect);
                var wrong = sessionIds.Count(sessionId =>
                {
                    var answer = answers.FirstOrDefault(a => a.StudentExamId == sessionId && a.QuestionId == q.Id);
                    if (q.Type == QuestionType.OpenEnded || IsOpenKind(q.InputKind))
                    {
                        return !TextMatches(answer?.TextAnswer, q.CorrectText, q.InputKind);
                    }
                    return answer?.SelectedOptionId == null || correct == null || answer.SelectedOptionId != correct.Id;
                });

                return new QuestionDifficultyDto
                {
                    QuestionId = q.Id,
                    Index = idx + 1,
                    Text = q.Text,
                    Participants = participants,
                    WrongCount = wrong,
                    Difficulty = wrong
                };
            }).ToList();
        }

        internal static bool TextMatches(string? given, string? expected, string? kind)
        {
            if (string.IsNullOrWhiteSpace(expected)) return false;
            if (string.IsNullOrWhiteSpace(given)) return false;
            var a = given.Trim().Replace(',', '.');
            var b = expected.Trim().Replace(',', '.');
            if (kind != null && (
                kind.Equals("Integer", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Decimal", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Number", StringComparison.OrdinalIgnoreCase))
                && decimal.TryParse(a, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var na)
                && decimal.TryParse(b, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var nb))
            {
                return na == nb;
            }

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
