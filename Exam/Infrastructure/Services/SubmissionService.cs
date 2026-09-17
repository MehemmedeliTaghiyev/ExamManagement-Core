using Exam.Core.Domain;
using Exam.Core.DTOs.Submission;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ExamDbContext _context;

        public SubmissionService(ExamDbContext context)
        {
            _context = context;
        }

        public async Task<StartExamResponseDto> StartExamAsync(StartExamDto dto)
        {
            var studentExists = await _context.Users.AnyAsync(u => u.Id == dto.StudentId);
            if (!studentExists)
            {
                throw new KeyNotFoundException($"ID-si {dto.StudentId} olan tələbə tapılmadı.");
            }

            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId);
            if (exam == null)
            {
                throw new KeyNotFoundException($"ID-si {dto.ExamId} olan imtahan tapılmadı.");
            }

            var start = exam.StartTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(exam.StartTime, DateTimeKind.Utc)
                : exam.StartTime.ToUniversalTime();
            if (DateTime.UtcNow < start)
            {
                throw new InvalidOperationException("İmtahan hələ başlamayıb. Suallar yalnız başlama vaxtında açılacaq.");
            }

            if (IsExamEnded(exam))
            {
                throw new InvalidOperationException("İmtahan artıq bitib.");
            }

            var existing = await _context.StudentExams
                .FirstOrDefaultAsync(se => se.StudentId == dto.StudentId && se.ExamId == dto.ExamId);

            if (existing != null)
            {
                return new StartExamResponseDto
                {
                    StudentExamId = existing.Id,
                    ExamId = existing.ExamId,
                    Status = existing.Status.ToString(),
                    AlreadySubmitted = existing.Status != StudentExamStatus.InProgress
                };
            }

            var studentExam = new StudentExam
            {
                ExamId = dto.ExamId,
                StudentId = dto.StudentId,
                StartedAt = DateTime.UtcNow,
                Status = StudentExamStatus.InProgress
            };

            _context.StudentExams.Add(studentExam);
            await _context.SaveChangesAsync();

            return new StartExamResponseDto
            {
                StudentExamId = studentExam.Id,
                ExamId = studentExam.ExamId,
                Status = studentExam.Status.ToString(),
                AlreadySubmitted = false
            };
        }

        public async Task<ExamResultDto?> SubmitExamAsync(SubmitExamDto dto)
        {
            StudentExam? studentExam = null;

            if (dto.StudentExamId > 0)
            {
                studentExam = await _context.StudentExams.FirstOrDefaultAsync(se => se.Id == dto.StudentExamId);
            }
            else if (dto.ExamId > 0 && dto.StudentId > 0)
            {
                var started = await StartExamAsync(new StartExamDto
                {
                    ExamId = dto.ExamId,
                    StudentId = dto.StudentId
                });
                studentExam = await _context.StudentExams.FirstOrDefaultAsync(se => se.Id == started.StudentExamId);
            }

            if (studentExam == null)
            {
                return null;
            }

            if (studentExam.Status != StudentExamStatus.InProgress)
            {
                return await GetStudentResultAsync(studentExam.Id);
            }

            return await GradeAndSaveAsync(studentExam, dto.Answers ?? new List<StudentAnswerDto>());
        }

        public async Task SaveProgressAsync(SubmitExamDto dto)
        {
            var studentExam = dto.StudentExamId > 0
                ? await _context.StudentExams.FirstOrDefaultAsync(se => se.Id == dto.StudentExamId)
                : null;

            if (studentExam == null || studentExam.Status != StudentExamStatus.InProgress)
            {
                return;
            }

            var previous = _context.StudentAnswers.Where(a => a.StudentExamId == studentExam.Id);
            _context.StudentAnswers.RemoveRange(previous);

            var rows = (dto.Answers ?? new List<StudentAnswerDto>())
                .Where(a => a.QuestionId > 0 && a.SelectedOptionId > 0)
                .Select(a => new StudentAnswer
                {
                    StudentExamId = studentExam.Id,
                    QuestionId = a.QuestionId,
                    SelectedOptionId = a.SelectedOptionId
                })
                .ToList();

            if (rows.Count > 0)
            {
                await _context.StudentAnswers.AddRangeAsync(rows);
            }

            await _context.SaveChangesAsync();
        }

        public async Task FinalizeExpiredSessionsAsync(int? studentId = null, int? examId = null)
        {
            var query = _context.StudentExams.Where(se => se.Status == StudentExamStatus.InProgress);
            if (studentId.HasValue && studentId.Value > 0)
            {
                query = query.Where(se => se.StudentId == studentId.Value);
            }
            if (examId.HasValue && examId.Value > 0)
            {
                query = query.Where(se => se.ExamId == examId.Value);
            }

            var openSessions = await query.ToListAsync();
            foreach (var session in openSessions)
            {
                var exam = await _context.Exams.FindAsync(session.ExamId);
                if (exam == null || !IsExamEnded(exam)) continue;

                var saved = await _context.StudentAnswers
                    .Where(a => a.StudentExamId == session.Id && a.SelectedOptionId != null)
                    .Select(a => new StudentAnswerDto
                    {
                        QuestionId = a.QuestionId,
                        SelectedOptionId = a.SelectedOptionId!.Value
                    })
                    .ToListAsync();

                await GradeAndSaveAsync(session, saved);
            }
        }

        private async Task<ExamResultDto?> GradeAndSaveAsync(StudentExam studentExam, List<StudentAnswerDto> answers)
        {
            var questions = await _context.Questions
                .Where(q => q.ExamId == studentExam.ExamId)
                .ToListAsync();

            var questionIds = questions.Select(q => q.Id).ToList();
            var allOptions = questionIds.Count == 0
                ? new List<QuestionOption>()
                : await _context.QuestionOptions
                    .Where(o => questionIds.Contains(o.QuestionId))
                    .ToListAsync();

            var userAnswers = new Dictionary<int, int>();
            var alreadySaved = await _context.StudentAnswers
                .Where(a => a.StudentExamId == studentExam.Id && a.SelectedOptionId != null)
                .ToListAsync();
            foreach (var saved in alreadySaved)
            {
                userAnswers[saved.QuestionId] = saved.SelectedOptionId!.Value;
            }
            foreach (var answer in answers)
            {
                if (answer.QuestionId > 0 && answer.SelectedOptionId > 0)
                {
                    userAnswers[answer.QuestionId] = answer.SelectedOptionId;
                }
            }

            int correctCount = 0;
            int unanswered = 0;
            var studentAnswersList = new List<StudentAnswer>(questions.Count);

            foreach (var question in questions)
            {
                var options = allOptions.Where(o => o.QuestionId == question.Id).ToList();
                int? selectedOptionId = userAnswers.TryGetValue(question.Id, out int optionId) ? optionId : null;
                var selected = options.FirstOrDefault(o => o.Id == selectedOptionId);
                var correct = options.FirstOrDefault(o => o.IsCorrect);
                bool isCorrect = selected != null && (selected.IsCorrect || (correct != null && selected.Id == correct.Id));

                if (isCorrect) correctCount++;
                if (!selectedOptionId.HasValue) unanswered++;

                studentAnswersList.Add(new StudentAnswer
                {
                    StudentExamId = studentExam.Id,
                    QuestionId = question.Id,
                    SelectedOptionId = selectedOptionId
                });
            }

            int totalQuestions = Math.Max(questions.Count, 1);
            int wrongCount = questions.Count - correctCount;
            decimal scorePercentage = questions.Count > 0
                ? Math.Round(((decimal)correctCount / questions.Count) * 100, 2)
                : 0;

            studentExam.SubmittedAt = DateTime.UtcNow;
            studentExam.FinalScore = scorePercentage;
            studentExam.Score = (int)Math.Round(scorePercentage);
            studentExam.CorrectAnswersCount = correctCount;
            studentExam.WrongAnswersCount = wrongCount;
            studentExam.UnansweredCount = unanswered;
            studentExam.Status = StudentExamStatus.Submitted;

            var previousAnswers = _context.StudentAnswers.Where(a => a.StudentExamId == studentExam.Id);
            _context.StudentAnswers.RemoveRange(previousAnswers);
            await _context.StudentAnswers.AddRangeAsync(studentAnswersList);

            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == studentExam.ExamId);
            if (exam != null)
            {
                exam.SubmissionsCount = await _context.StudentExams.CountAsync(se =>
                    se.ExamId == exam.Id && se.Id != studentExam.Id && se.Status != StudentExamStatus.InProgress) + 1;
            }

            await _context.SaveChangesAsync();
            return await GetStudentResultAsync(studentExam.Id);
        }

        public async Task<ExamResultDto?> GetStudentResultAsync(int studentExamId)
        {
            var se = await _context.StudentExams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == studentExamId);
            if (se == null) return null;
            var all = await GetExamSubmissionsAsync(se.ExamId);
            return all.FirstOrDefault(x => x.StudentExamId == studentExamId);
        }

        public async Task<IReadOnlyList<ExamResultDto>> GetStudentHistoryAsync(int studentId)
        {
            await FinalizeExpiredSessionsAsync(studentId);
            return await GetExamSubmissionsAsyncInternal(se =>
                se.StudentId == studentId && se.Status != StudentExamStatus.InProgress);
        }

        public async Task<IReadOnlyList<ExamResultDto>> GetExamSubmissionsAsync(int examId)
        {
            return await GetExamSubmissionsAsyncInternal(se =>
                se.ExamId == examId && se.Status != StudentExamStatus.InProgress);
        }

        public async Task<ExamReviewDto?> GetExamReviewAsync(int studentExamId, int requesterId, bool isStaff)
        {
            var studentExam = await _context.StudentExams.FirstOrDefaultAsync(se => se.Id == studentExamId);
            if (studentExam == null) return null;
            if (!isStaff && studentExam.StudentId != requesterId) return null;

            await FinalizeExpiredSessionsAsync(studentExam.StudentId, studentExam.ExamId);
            studentExam = await _context.StudentExams.FirstOrDefaultAsync(se => se.Id == studentExamId);
            if (studentExam == null) return null;

            return await BuildReviewAsync(studentExam, isStaff);
        }

        public async Task<ExamReviewDto?> GetExamReviewByExamAsync(int examId, int requesterId, bool isStaff)
        {
            await FinalizeExpiredSessionsAsync(isStaff ? null : requesterId, examId);

            if (isStaff)
            {
                var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
                if (exam == null) return null;
                var board = await GetExamSubmissionsAsync(examId);
                return new ExamReviewDto
                {
                    ExamEnded = IsExamEnded(exam),
                    ReviewAvailable = true,
                    Result = new ExamResultDto
                    {
                        ExamId = exam.Id,
                        ExamTitle = exam.Title,
                        TotalQuestions = await _context.Questions.CountAsync(q => q.ExamId == examId)
                    },
                    Questions = IsExamEnded(exam) ? await BuildPaperAsync(examId, null) : new List<QuestionReviewDto>(),
                    Leaderboard = IsExamEnded(exam) ? board.ToList() : new List<ExamResultDto>()
                };
            }

            var own = await _context.StudentExams
                .FirstOrDefaultAsync(se => se.ExamId == examId && se.StudentId == requesterId);

            if (own == null)
            {
                var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
                if (exam == null || !IsExamEnded(exam)) return null;
                return new ExamReviewDto
                {
                    ExamEnded = true,
                    ReviewAvailable = true,
                    Result = new ExamResultDto { ExamId = exam.Id, ExamTitle = exam.Title },
                    Questions = await BuildPaperAsync(examId, null),
                    Leaderboard = (await GetExamSubmissionsAsync(examId)).ToList()
                };
            }
            return await BuildReviewAsync(own, false);
        }

        private async Task<ExamReviewDto> BuildReviewAsync(StudentExam studentExam, bool isStaff)
        {
            var exam = await _context.Exams.FirstAsync(e => e.Id == studentExam.ExamId);
            var ended = IsExamEnded(exam);
            var reviewAvailable = isStaff || ended;
            var result = await GetStudentResultAsync(studentExam.Id) ?? new ExamResultDto();
            result.ReviewAvailable = reviewAvailable;

            var review = new ExamReviewDto
            {
                Result = result,
                ExamEnded = ended,
                ReviewAvailable = reviewAvailable,
                Leaderboard = ended
                    ? (await GetExamSubmissionsAsync(studentExam.ExamId)).ToList()
                    : new List<ExamResultDto>(),
                Questions = await BuildPaperAsync(studentExam.ExamId, studentExam.Id),
                PdfFilePath = exam.PdfFilePath,
                PdfFileUrl = exam.PdfFilePath
            };

            if (!ended)
            {
                foreach (var question in review.Questions)
                {
                    question.IsCorrect = false;
                    question.CorrectOptionId = null;
                    question.CorrectText = null;
                    foreach (var option in question.Options)
                    {
                        option.IsCorrect = false;
                    }
                }
            }

            return review;
        }

        private async Task<List<QuestionReviewDto>> BuildPaperAsync(int examId, int? studentExamId)
        {
            var questions = await _context.Questions
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            var questionIds = questions.Select(q => q.Id).ToList();
            var options = questionIds.Count == 0
                ? new List<QuestionOption>()
                : await _context.QuestionOptions
                    .Where(o => questionIds.Contains(o.QuestionId))
                    .ToListAsync();

            var answers = studentExamId == null
                ? new List<StudentAnswer>()
                : await _context.StudentAnswers
                    .Where(a => a.StudentExamId == studentExamId)
                    .ToListAsync();

            return questions.Select((q, idx) =>
            {
                var qOptions = options.Where(o => o.QuestionId == q.Id).ToList();
                var saved = answers.FirstOrDefault(a => a.QuestionId == q.Id);
                var correct = qOptions.FirstOrDefault(o => o.IsCorrect);
                var selectedOpt = qOptions.FirstOrDefault(o => o.Id == saved?.SelectedOptionId);
                var isCorrect = selectedOpt != null && (selectedOpt.IsCorrect || (correct != null && selectedOpt.Id == correct.Id));

                return new QuestionReviewDto
                {
                    QuestionId = q.Id,
                    Index = idx + 1,
                    Text = q.Text,
                    SelectedOptionId = saved?.SelectedOptionId,
                    SelectedText = selectedOpt?.OptionText,
                    CorrectOptionId = correct?.Id,
                    CorrectText = correct?.OptionText,
                    IsCorrect = isCorrect,
                    Unanswered = saved?.SelectedOptionId == null,
                    Options = qOptions.Select(o => new QuestionOptionReviewDto
                    {
                        Id = o.Id,
                        Text = o.OptionText,
                        IsCorrect = o.IsCorrect,
                        IsSelected = saved?.SelectedOptionId == o.Id
                    }).ToList()
                };
            }).ToList();
        }

        private async Task<List<ExamResultDto>> GetExamSubmissionsAsyncInternal(
            System.Linq.Expressions.Expression<Func<StudentExam, bool>> predicate)
        {
            var rows = await (
                from se in _context.StudentExams.Where(predicate)
                join exam in _context.Exams on se.ExamId equals exam.Id
                join student in _context.Users on se.StudentId equals student.Id
                select new
                {
                    se.Id,
                    se.ExamId,
                    se.StudentId,
                    StudentName = student.FullName,
                    exam.Title,
                    se.CorrectAnswersCount,
                    se.WrongAnswersCount,
                    se.UnansweredCount,
                    se.FinalScore,
                    StudentStatus = se.Status,
                    se.StartedAt,
                    se.SubmittedAt,
                    exam.EndTime,
                    ExamLiveStatus = exam.Status
                }).ToListAsync();

            var questionCounts = await _context.Questions
                .GroupBy(q => q.ExamId)
                .Select(g => new { ExamId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ExamId, x => x.Count);

            var grouped = rows
                .GroupBy(r => r.ExamId)
                .SelectMany(g => g
                    .OrderByDescending(r => r.FinalScore)
                    .ThenBy(r =>
                    {
                        var submitted = r.SubmittedAt ?? DateTime.MaxValue;
                        return submitted - r.StartedAt;
                    })
                    .ThenBy(r => r.SubmittedAt ?? DateTime.MaxValue)
                    .Select((r, index) =>
                    {
                        var submitted = r.SubmittedAt ?? DateTime.UtcNow;
                        var duration = (int)Math.Max(0, (submitted - r.StartedAt).TotalSeconds);
                        return new ExamResultDto
                        {
                            Id = r.Id,
                            StudentExamId = r.Id,
                            ExamId = r.ExamId,
                            StudentId = r.StudentId,
                            StudentName = r.StudentName,
                            ExamTitle = r.Title,
                            TotalQuestions = questionCounts.GetValueOrDefault(r.ExamId),
                            CorrectAnswersCount = r.CorrectAnswersCount,
                            WrongAnswersCount = r.WrongAnswersCount,
                            UnansweredCount = r.UnansweredCount,
                            Score = r.FinalScore,
                            Percent = r.FinalScore,
                            Rank = index + 1,
                            Status = r.StudentStatus.ToString(),
                            StartedAt = r.StartedAt,
                            SubmittedAt = submitted,
                            DurationSeconds = duration,
                            ReviewAvailable = r.ExamLiveStatus == ExamStatus.Finished || r.EndTime <= DateTime.UtcNow
                        };
                    }))
                .ToList();

            return grouped;
        }

        private static bool IsExamEnded(Core.Domain.Exam exam)
        {
            var start = exam.StartTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(exam.StartTime, DateTimeKind.Utc)
                : exam.StartTime.ToUniversalTime();
            var end = exam.EndTime.Year >= 2000
                ? (exam.EndTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(exam.EndTime, DateTimeKind.Utc)
                    : exam.EndTime.ToUniversalTime())
                : start.AddMinutes(Math.Max(exam.DurationMinutes, 1));

            if (DateTime.UtcNow >= end)
            {
                exam.Status = ExamStatus.Finished;
                exam.EndTime = end;
                return true;
            }

            if (exam.Status == ExamStatus.Finished)
            {
                exam.Status = ExamStatus.Live;
            }

            return false;
        }
    }
}
