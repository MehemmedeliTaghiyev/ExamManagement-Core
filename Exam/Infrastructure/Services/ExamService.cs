using Exam.Core.Domain;
using Exam.Core.DTOs.Exam;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Exam.Infrastructure;
using Microsoft.EntityFrameworkCore;
using PagedResult = Exam.Core.DTOs.Exam.PagedResult<Exam.Core.DTOs.Exam.ExamResponseDto>;

namespace Exam.Infrastructure.Services
{
    public class ExamService : IExamService
    {
        private readonly ExamDbContext _context;
        private readonly IFileStorage _files;
        private readonly IQuestionService _questions;
        private readonly CurrentTenant _tenant;

        public ExamService(ExamDbContext context, IFileStorage files, IQuestionService questions, CurrentTenant tenant)
        {
            _context = context;
            _files = files;
            _questions = questions;
            _tenant = tenant;
        }

        private static DateTime NormalizeTime(DateTime value, DateTime fallback)
        {
            if (value.Year < 2000) return fallback;
            return value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
        }

        private static Core.Enums.ExamStatus ResolveStatus(DateTime start, DateTime end, DateTime now)
        {
            if (now >= end) return Core.Enums.ExamStatus.Finished;
            if (now < start) return Core.Enums.ExamStatus.Scheduled;
            return Core.Enums.ExamStatus.Live;
        }

        public async Task SyncExamStatusesAsync()
        {
            var now = DateTime.UtcNow;
            var exams = await _context.Exams.ToListAsync();
            var changed = false;

            foreach (var exam in exams)
            {
                if (exam.Status == Core.Enums.ExamStatus.Draft)
                {
                    continue;
                }
                if (exam.StartTime.Year < 2000)
                {
                    exam.StartTime = exam.CreatedAt.Year > 2000 ? exam.CreatedAt : now;
                    changed = true;
                }

                var start = NormalizeTime(exam.StartTime, now);
                var durationEnd = start.AddMinutes(Math.Max(exam.DurationMinutes, 1));
                var end = exam.EndTime.Year >= 2000 ? NormalizeTime(exam.EndTime, durationEnd) : durationEnd;
                if (end <= start)
                {
                    end = durationEnd;
                }

                if (exam.StartTime != start)
                {
                    exam.StartTime = start;
                    changed = true;
                }

                if (exam.EndTime != end)
                {
                    exam.EndTime = end;
                    changed = true;
                }

                var next = ResolveStatus(start, end, now);
                if (exam.Status != next)
                {
                    exam.Status = next;
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Exam.Core.Domain.Exam?> CreateExamAsync(CreateExamDto dto)
        {
            var subjectExists = await _context.Subjects.AnyAsync(s => s.Id == dto.SubjectId);
            if (!subjectExists)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var start = NormalizeTime(dto.StartTime, now);
            var end = NormalizeTime(dto.EndTime, start.AddMinutes(Math.Max(dto.DurationMinutes, 1)));
            if (end <= start)
            {
                end = start.AddMinutes(Math.Max(dto.DurationMinutes, 1));
            }

            var isDraft = dto.IsDraft
                || string.Equals(dto.Status, "Draft", StringComparison.OrdinalIgnoreCase);

            var newExam = new Exam.Core.Domain.Exam
            {
                SubjectId = dto.SubjectId,
                Title = dto.Title,
                DurationMinutes = dto.DurationMinutes,
                TotalQuestions = dto.TotalQuestions,
                StartTime = start,
                EndTime = end,
                Status = isDraft ? Core.Enums.ExamStatus.Draft : ResolveStatus(start, end, now),
                SubmissionsCount = 0,
                CreatedAt = now,
                TeacherId = _tenant.IsAdmin ? _tenant.UserId : _tenant.TeacherScopeId ?? _tenant.UserId
            };

            _context.Exams.Add(newExam);
            await _context.SaveChangesAsync();

            return newExam;
        }

        public async Task<ExamCardDto?> GetExamCardByIdAsync(int examId)
        {
            await SyncExamStatusesAsync();
            // Joining Exam with Subject to populate SubjectName
            var query = from exam in _tenant.VisibleExams()
                        where exam.Id == examId
                        join subject in _context.Subjects on exam.SubjectId equals subject.Id
                        join teacher in _context.Users on exam.TeacherId equals teacher.Id into tg
                        from teacher in tg.DefaultIfEmpty()
                        select new ExamCardDto
                        {
                            Id = exam.Id,
                            Title = exam.Title,
                            SubjectId = exam.SubjectId,
                            SubjectName = subject.Name,
                            TotalQuestions = exam.TotalQuestions,
                            DurationMinutes = exam.DurationMinutes,
                            SubmissionsCount = exam.SubmissionsCount,
                            StartTime = exam.StartTime,
                            EndTime = exam.EndTime,
                            Status = exam.Status.ToString(),
                            PdfFilePath = exam.PdfFilePath,
                            TeacherId = exam.TeacherId,
                            TeacherName = teacher != null ? teacher.FullName : null
                        };

            var card = await query.FirstOrDefaultAsync();
            return AttachUrl(card);
        }

        public async Task<IReadOnlyList<ExamCardDto>> GetStudentExamCardsAsync()
        {
            await SyncExamStatusesAsync();
            var query = from exam in _tenant.VisibleExams()
                        where exam.Status != Core.Enums.ExamStatus.Draft
                        join subject in _context.Subjects on exam.SubjectId equals subject.Id
                        join teacher in _context.Users on exam.TeacherId equals teacher.Id into tg
                        from teacher in tg.DefaultIfEmpty()
                        select new ExamCardDto
                        {
                            Id = exam.Id,
                            Title = exam.Title,
                            SubjectId = exam.SubjectId,
                            SubjectName = subject.Name,
                            TotalQuestions = exam.TotalQuestions,
                            DurationMinutes = exam.DurationMinutes,
                            SubmissionsCount = exam.SubmissionsCount,
                            StartTime = exam.StartTime,
                            EndTime = exam.EndTime,
                            Status = exam.Status.ToString(),
                            PdfFilePath = exam.PdfFilePath,
                            TeacherId = exam.TeacherId,
                            TeacherName = teacher != null ? teacher.FullName : null
                        };

            var cards = await query.ToListAsync();
            cards.ForEach(c => AttachUrl(c));
            return cards;
        }

        public async Task<bool> UpdateExamPdfPathAsync(int examId, string relativePath)
        {
            var exam = await _context.Exams.FindAsync(examId);
            if (!_tenant.CanAccessExam(exam)) return false;

            exam.PdfFilePath = relativePath;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ExamResponseDto?> SaveExamPdfAndSlotsAsync(
            int examId,
            Stream content,
            string fileName,
            string contentType,
            int questionCount)
        {
            var exam = await _context.Exams.FindAsync(examId);
            if (exam == null) return null;
            if (exam.TeacherId == null && _tenant.IsTeacher && _tenant.UserId > 0)
            {
                exam.TeacherId = _tenant.UserId;
            }
            if (!_tenant.CanAccessExam(exam)) return null;

            questionCount = Math.Clamp(questionCount, 1, 200);

            if (!string.IsNullOrWhiteSpace(exam.PdfFilePath))
            {
                await _files.DeleteAsync(exam.PdfFilePath);
            }

            var stored = await _files.SaveAsync(content, fileName, contentType, $"exams/{examId}");
            exam.PdfFilePath = stored.Key;
            exam.TotalQuestions = Math.Max(exam.TotalQuestions, questionCount);
            await _context.SaveChangesAsync();

            await _questions.EnsureChoiceSlotsAsync(examId, questionCount);
            return await GetExamByIdAsync(examId);
        }

        private ExamCardDto? AttachUrl(ExamCardDto? dto)
        {
            if (dto == null) return null;
            dto.PdfFileUrl = _files.ToPublicUrl(dto.PdfFilePath);
            return dto;
        }

        private ExamResponseDto? AttachUrl(ExamResponseDto? dto)
        {
            if (dto == null) return null;
            dto.PdfFileUrl = _files.ToPublicUrl(dto.PdfFilePath);
            return dto;
        }

        public async Task<ExamResponseDto?> GetExamByIdAsync(int id)
        {
            await SyncExamStatusesAsync();
            var dto = await (from exam in _tenant.VisibleExams()
                          join subject in _context.Subjects on exam.SubjectId equals subject.Id
                          join teacher in _context.Users on exam.TeacherId equals teacher.Id into tg
                          from teacher in tg.DefaultIfEmpty()
                          where exam.Id == id
                          select new ExamResponseDto
                          {
                              Id = exam.Id,
                              Title = exam.Title,
                              SubjectId = exam.SubjectId,
                              SubjectName = subject.Name,
                              DurationMinutes = exam.DurationMinutes,
                              TotalQuestions = exam.TotalQuestions,
                              SubmissionsCount = exam.SubmissionsCount,
                              PdfFilePath = exam.PdfFilePath,
                              TeacherId = exam.TeacherId,
                              TeacherName = teacher != null ? teacher.FullName : null,
                              Status = exam.Status.ToString(),
                              StartTime = exam.StartTime,
                              EndTime = exam.EndTime,
                              CreatedAt = exam.CreatedAt
                          }).FirstOrDefaultAsync();
            return AttachUrl(dto);
        }

        public async Task<ExamResponseDto?> GetExamResponseByIdAsync(int id)
        {
            await SyncExamStatusesAsync();
            var dto = await (from exam in _tenant.VisibleExams()
                          join subject in _context.Subjects on exam.SubjectId equals subject.Id
                          join teacher in _context.Users on exam.TeacherId equals teacher.Id into tg
                          from teacher in tg.DefaultIfEmpty()
                          where exam.Id == id
                          select new ExamResponseDto
                          {
                              Id = exam.Id,
                              Title = exam.Title,
                              SubjectId = exam.SubjectId,
                              SubjectName = subject.Name,
                              DurationMinutes = exam.DurationMinutes,
                              TotalQuestions = exam.TotalQuestions,
                              PdfFilePath = exam.PdfFilePath,
                              TeacherId = exam.TeacherId,
                              TeacherName = teacher != null ? teacher.FullName : null,
                              Status = exam.Status.ToString(),
                              StartTime = exam.StartTime,
                              EndTime = exam.EndTime,
                              CreatedAt = exam.CreatedAt
                          }).FirstOrDefaultAsync();
            return AttachUrl(dto);
        }

        public async Task<bool> UpdateExamAsync(int id, UpdateExamDto dto)
        {
            // Find the Exam entity directly from the DbContext
            var exam = await _context.Exams.FindAsync(id);
            if (!_tenant.CanAccessExam(exam))
                return false;

            var subjectExists = await _context.Subjects.AnyAsync(s => s.Id == dto.SubjectId);
            if (!subjectExists)
                return false;

            exam.SubjectId = dto.SubjectId;
            exam.Title = dto.Title;
            exam.DurationMinutes = dto.DurationMinutes;
            exam.TotalQuestions = dto.TotalQuestions;
            exam.StartTime = dto.StartTime;
            exam.EndTime = dto.EndTime;
            exam.Status = Enum.Parse<Core.Enums.ExamStatus>(dto.Status, ignoreCase: true);
            _context.Exams.Update(exam);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteExamAsync(int id)
        {
            // 1. İmtahanı bazadan axtarırıq
            var exam = await _context.Exams.FindAsync(id);
            if (!_tenant.CanAccessExam(exam))
                return false;

            // 2. Əgər imtahana bağlı PDF faylı varsa, fiziki olaraq diskdən silirik
            if (!string.IsNullOrEmpty(exam.PdfFilePath))
            {
                await _files.DeleteAsync(exam.PdfFilePath);
            }

            var questionIds = await _context.Questions
                .Where(q => q.ExamId == id)
                .Select(q => q.Id)
                .ToListAsync();
            var sessionIds = await _context.StudentExams
                .Where(se => se.ExamId == id)
                .Select(se => se.Id)
                .ToListAsync();

            var answers = _context.StudentAnswers.Where(a =>
                sessionIds.Contains(a.StudentExamId) || questionIds.Contains(a.QuestionId));
            _context.StudentAnswers.RemoveRange(answers);

            var options = _context.QuestionOptions.Where(o => questionIds.Contains(o.QuestionId));
            _context.QuestionOptions.RemoveRange(options);

            var questions = _context.Questions.Where(q => q.ExamId == id);
            _context.Questions.RemoveRange(questions);

            var sessions = _context.StudentExams.Where(se => se.ExamId == id);
            _context.StudentExams.RemoveRange(sessions);

            _context.Exams.Remove(exam);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<PagedResult<ExamResponseDto>> GetAllExamsAsync(ExamQueryParameters queryParameters)
        {
            await SyncExamStatusesAsync();
            var examsQuery = _tenant.VisibleExams();
            if (_tenant.IsStudent)
            {
                examsQuery = examsQuery.Where(e => e.Status != Core.Enums.ExamStatus.Draft);
            }
            var query = from exam in examsQuery
                        join subject in _context.Subjects on exam.SubjectId equals subject.Id
                        join teacher in _context.Users on exam.TeacherId equals teacher.Id into tg
                        from teacher in tg.DefaultIfEmpty()
                        select new ExamResponseDto
                        {
                            Id = exam.Id,
                            Title = exam.Title,
                            SubjectId = exam.SubjectId,
                            SubjectName = subject.Name,
                            DurationMinutes = exam.DurationMinutes,
                            TotalQuestions = exam.TotalQuestions,
                            SubmissionsCount = exam.SubmissionsCount,
                            PdfFilePath = exam.PdfFilePath,
                            TeacherId = exam.TeacherId,
                            TeacherName = teacher != null ? teacher.FullName : null,
                            Status = exam.Status.ToString(),
                            StartTime = exam.StartTime,
                            EndTime = exam.EndTime,
                            CreatedAt = exam.CreatedAt
                        };

            // 2. Search (Axtarış)
            if (!string.IsNullOrWhiteSpace(queryParameters.Search))
            {
                var searchTerm = queryParameters.Search.Trim().ToLower();
                query = query.Where(e => e.Title.ToLower().Contains(searchTerm));
            }

            // 3. Filtering (Fənnə görə)
            if (queryParameters.SubjectId.HasValue)
            {
                query = query.Where(e => e.SubjectId == queryParameters.SubjectId.Value);
            }

            // 4. Filtering (Statusa görə)
            if (!string.IsNullOrWhiteSpace(queryParameters.Status))
            {
                var statusTerm = queryParameters.Status.Trim().ToLower();
                query = query.Where(e => e.Status.ToLower() == statusTerm);
            }

            // 5. Ümumi uyğun gələn sayını alırıq
            var totalCount = await query.CountAsync();

            // 6. Sıralama və Pagination (Səhifələmə)
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((queryParameters.PageNumber - 1) * queryParameters.PageSize)
                .Take(queryParameters.PageSize)
                .ToListAsync();
            items.ForEach(i => AttachUrl(i));

            // 7. Qaydılacaq obyekt
            return new PagedResult<ExamResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = queryParameters.PageNumber,
                PageSize = queryParameters.PageSize
            };
        }

        public async Task<QuestionResponseDto?> AddQuestionToExamAsync(int examId, CreateQuestionDto dto)
        {
            var exam = await _context.Exams.FindAsync(examId);
            if (!_tenant.CanAccessExam(exam)) return null;

            // 1. Save Question entity
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
            await _context.SaveChangesAsync(); // Generates question.Id

            // 2. Save options linked by QuestionId
            var optionsList = new List<QuestionOption>();

            if (dto.Options != null && dto.Options.Any() && dto.Type != QuestionType.OpenEnded)
            {
                foreach (var optDto in dto.Options)
                {
                    var option = new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = string.IsNullOrWhiteSpace(optDto.OptionText) ? (optDto.Text ?? string.Empty) : optDto.OptionText,
                        IsCorrect = optDto.IsCorrect
                    };
                    optionsList.Add(option);
                }

                _context.QuestionOptions.AddRange(optionsList);
                await _context.SaveChangesAsync();
            }

            // 3. Return response payload
            return new QuestionResponseDto
            {
                Id = question.Id,
                ExamId = question.ExamId,
                Text = question.Text,
                Points = question.Points,
                Type = question.Type,
                InputKind = question.InputKind,
                CorrectText = question.CorrectText,
                Options = optionsList.Select(o => new QuestionOptionResponseDto
                {
                    Id = o.Id,
                    QuestionId = o.QuestionId,
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect
                }).ToList()
            };
        }

    }
}
