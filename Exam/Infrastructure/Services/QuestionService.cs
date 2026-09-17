using Exam.Core.Domain;
using Exam.Core.DTOs.Exam;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Services
{
    public class QuestionService : IQuestionService
    {
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
                Type = dto.Type
            };

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();

            if (dto.Options != null && dto.Options.Any())
            {
                var options = dto.Options.Select(o => new QuestionOption
                {
                    QuestionId = question.Id,
                    OptionText = string.IsNullOrWhiteSpace(o.OptionText) ? (o.Text ?? string.Empty) : o.OptionText,
                    IsCorrect = o.IsCorrect
                }).ToList();

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

            var created = 0;
            for (var i = existing.Count + 1; i <= count; i++)
            {
                var question = new Question
                {
                    ExamId = examId,
                    Text = $"Sual {i}",
                    Points = 1,
                    Type = QuestionType.SingleChoice
                };
                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                _context.QuestionOptions.AddRange(new[]
                {
                    new QuestionOption { QuestionId = question.Id, OptionText = "A", IsCorrect = false },
                    new QuestionOption { QuestionId = question.Id, OptionText = "B", IsCorrect = false },
                    new QuestionOption { QuestionId = question.Id, OptionText = "C", IsCorrect = false },
                    new QuestionOption { QuestionId = question.Id, OptionText = "D", IsCorrect = false },
                });
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
            var questionIds = await _context.Questions
                .Where(q => q.ExamId == examId)
                .Select(q => q.Id)
                .ToListAsync();

            var options = await _context.QuestionOptions
                .Where(o => questionIds.Contains(o.QuestionId))
                .ToListAsync();

            foreach (var item in items)
            {
                if (!questionIds.Contains(item.QuestionId)) continue;
                var qOpts = options.Where(o => o.QuestionId == item.QuestionId).OrderBy(o => o.Id).ToList();
                var idx = LetterToIndex(item.CorrectLetter);
                if (idx < 0 || idx >= qOpts.Count) continue;
                for (var i = 0; i < qOpts.Count; i++)
                {
                    qOpts[i].IsCorrect = i == idx;
                }
            }

            await _context.SaveChangesAsync();
        }

        private static int LetterToIndex(string? letter)
        {
            if (string.IsNullOrWhiteSpace(letter)) return -1;
            var ch = char.ToUpperInvariant(letter.Trim()[0]);
            return ch is >= 'A' and <= 'D' ? ch - 'A' : -1;
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
    }
}
