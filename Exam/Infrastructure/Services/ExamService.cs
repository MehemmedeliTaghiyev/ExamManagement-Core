using Exam.Core.Domain;
using Exam.Core.DTOs.Exam;
using Exam.Core.Enums;
using Exam.Core.Exceptions;
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

            var (isPublic, visibility) = ExamSharing.Normalize(dto.IsPublic, dto.Visibility);
            ExamSharing.EnsurePublicAllowsTopics(isPublic, TopicHints(dto.Topic, null));

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
                TeacherId = _tenant.IsAdmin ? _tenant.UserId : _tenant.TeacherScopeId ?? _tenant.UserId,
                IsPublic = isPublic,
                Visibility = visibility,
                ShareGroupName = ShareGroupOf(visibility, dto.ShareGroupName ?? dto.GroupName),
                Topic = string.IsNullOrWhiteSpace(dto.Topic) ? null : dto.Topic.Trim()
            };

            _context.Exams.Add(newExam);
            await _context.SaveChangesAsync();
            await ReplaceAssigneesAsync(newExam, visibility, dto.StudentIds);
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
                            TeacherName = teacher != null ? teacher.FullName : null,
                            IsPublic = exam.IsPublic,
                            Visibility = exam.Visibility ?? (exam.IsPublic ? ExamSharing.Public : ExamSharing.Private),
                            Topic = exam.Topic,
                            ShareGroupName = exam.ShareGroupName
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
                            TeacherName = teacher != null ? teacher.FullName : null,
                            IsPublic = exam.IsPublic,
                            Visibility = exam.Visibility ?? (exam.IsPublic ? ExamSharing.Public : ExamSharing.Private),
                            Topic = exam.Topic,
                            ShareGroupName = exam.ShareGroupName
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
                              CreatedAt = exam.CreatedAt,
                              IsPublic = exam.IsPublic,
                              Visibility = exam.Visibility ?? (exam.IsPublic ? ExamSharing.Public : ExamSharing.Private),
                              Topic = exam.Topic,
                              ShareGroupName = exam.ShareGroupName
                          }).FirstOrDefaultAsync();
            return await AttachShareAsync(dto);
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
                              CreatedAt = exam.CreatedAt,
                              IsPublic = exam.IsPublic,
                              Visibility = exam.Visibility ?? (exam.IsPublic ? ExamSharing.Public : ExamSharing.Private),
                              Topic = exam.Topic,
                              ShareGroupName = exam.ShareGroupName
                          }).FirstOrDefaultAsync();
            return await AttachShareAsync(dto);
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
            if (dto.IsPublic.HasValue || !string.IsNullOrWhiteSpace(dto.Visibility) || dto.StudentIds != null
                || dto.GroupName != null || dto.ShareGroupName != null)
            {
                await ApplyShareAsync(
                    exam,
                    dto.IsPublic,
                    dto.Visibility,
                    dto.ShareGroupName ?? dto.GroupName,
                    dto.StudentIds,
                    dto.Topic);
            }
            else if (!string.IsNullOrWhiteSpace(dto.Topic))
            {
                exam.Topic = dto.Topic.Trim();
            }
            _context.Exams.Update(exam);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ShareExamAsync(int id, ShareExamDto dto)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (!_tenant.CanAccessExam(exam))
                return false;

            await ApplyShareAsync(
                exam,
                dto.IsPublic,
                dto.Visibility,
                dto.ShareGroupName ?? dto.GroupName,
                dto.StudentIds,
                extraTopic: null);
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
            var directoryPublic = queryParameters.IsPublic == true
                || string.Equals(queryParameters.Visibility, ExamSharing.Public, StringComparison.OrdinalIgnoreCase);
            IQueryable<Exam.Core.Domain.Exam> examsQuery;
            if (directoryPublic)
            {
                examsQuery = _context.Exams.Where(e => e.IsPublic && e.Status != Core.Enums.ExamStatus.Draft);
                if (queryParameters.TeacherId.HasValue)
                    examsQuery = examsQuery.Where(e => e.TeacherId == queryParameters.TeacherId.Value);
            }
            else
            {
                examsQuery = _tenant.VisibleExams();
                if (_tenant.IsStudent || (!_tenant.IsAdmin && !_tenant.IsTeacher))
                    examsQuery = examsQuery.Where(e => e.Status != Core.Enums.ExamStatus.Draft);
                if (queryParameters.TeacherId.HasValue)
                    examsQuery = examsQuery.Where(e => e.TeacherId == queryParameters.TeacherId.Value);
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
                            CreatedAt = exam.CreatedAt,
                            IsPublic = exam.IsPublic,
                            Visibility = exam.Visibility ?? (exam.IsPublic ? ExamSharing.Public : ExamSharing.Private),
                            Topic = exam.Topic,
                            ShareGroupName = exam.ShareGroupName
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
            var pageSize = queryParameters.PageSize;
            if (queryParameters.TeacherId.HasValue && pageSize < 50)
                pageSize = Math.Min(200, Math.Max(totalCount, 50));

            // 6. Sıralama və Pagination (Səhifələmə)
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((queryParameters.PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            items.ForEach(i => AttachUrl(i));

            // 7. Qaydılacaq obyekt
            return new PagedResult<ExamResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = queryParameters.PageNumber,
                PageSize = pageSize
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
            var response = new QuestionResponseDto
            {
                Id = question.Id,
                ExamId = question.ExamId,
                Text = question.Text,
                Points = question.Points,
                Type = question.Type,
                InputKind = question.InputKind,
                CorrectText = question.CorrectText,
                SourceQnum = question.SourceQnum,
                Options = optionsList.Select(o => new QuestionOptionResponseDto
                {
                    Id = o.Id,
                    QuestionId = o.QuestionId,
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect
                }).ToList()
            };
            response.AttachFigure(question.FigureUrl);
            return response;
        }

        private async Task<ExamResponseDto?> AttachShareAsync(ExamResponseDto? dto)
        {
            if (dto == null) return null;
            AttachUrl(dto);
            if (_tenant.IsAdmin || _tenant.IsTeacher)
            {
                dto.StudentIds = await _context.ExamAssignees
                    .Where(a => a.ExamId == dto.Id)
                    .Select(a => a.StudentId)
                    .ToListAsync();
            }
            return dto;
        }

        private static IEnumerable<string?> TopicHints(string? topic, IEnumerable<string?>? extra)
        {
            if (extra != null)
            {
                foreach (var item in extra)
                    yield return item;
            }
            yield return topic;
        }

        private static string? ShareGroupOf(string visibility, string? groupName)
        {
            var name = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
            if (visibility != ExamSharing.Group)
                return visibility == ExamSharing.Public || visibility == ExamSharing.Private ? null : name;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("GROUP_REQUIRED|Qrup seçin. Qrup paylaşımı üçün qrup adı lazımdır.");
            return name;
        }

        private async Task ApplyShareAsync(
            Exam.Core.Domain.Exam exam,
            bool? isPublic,
            string? visibility,
            string? groupName,
            List<int>? studentIds,
            string? extraTopic)
        {
            var (pub, vis) = ExamSharing.Normalize(isPublic ?? exam.IsPublic, visibility ?? exam.Visibility);
            await ExamSharing.EnsurePublicSingleTopicAsync(
                _context,
                exam.Id,
                pub,
                TopicHints(extraTopic ?? exam.Topic, null));
            exam.IsPublic = pub;
            exam.Visibility = vis;
            exam.ShareGroupName = ShareGroupOf(vis, groupName ?? exam.ShareGroupName);
            if (!string.IsNullOrWhiteSpace(extraTopic))
            {
                exam.Topic = extraTopic.Trim();
            }
            else
            {
                var topics = await ExamSharing.TopicsForExamAsync(_context, exam.Id);
                if (topics.Count == 1)
                    exam.Topic = topics[0];
            }

            await ReplaceAssigneesAsync(exam, vis, studentIds);
        }

        private async Task ReplaceAssigneesAsync(Exam.Core.Domain.Exam exam, string visibility, List<int>? studentIds)
        {
            if (studentIds == null && visibility == ExamSharing.Students)
                throw new InvalidOperationException("STUDENTS_REQUIRED|Ən azı bir şagird seçin.");

            if (studentIds == null && visibility != ExamSharing.Students)
            {
                if (visibility is ExamSharing.Public or ExamSharing.Private or ExamSharing.Group)
                {
                    var stale = _context.ExamAssignees.Where(a => a.ExamId == exam.Id);
                    _context.ExamAssignees.RemoveRange(stale);
                }
                return;
            }

            var existing = _context.ExamAssignees.Where(a => a.ExamId == exam.Id);
            _context.ExamAssignees.RemoveRange(existing);
            if (visibility != ExamSharing.Students)
                return;

            var ids = (studentIds ?? []).Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                throw new InvalidOperationException("STUDENTS_REQUIRED|Ən azı bir şagird seçin.");

            var allowed = await _context.Users.IgnoreQueryFilters()
                .Where(u => ids.Contains(u.Id)
                    && u.Role == UserRole.Student
                    && !u.IsDeleted
                    && u.TeacherId == exam.TeacherId)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var id in allowed)
            {
                _context.ExamAssignees.Add(new ExamAssignee
                {
                    ExamId = exam.Id,
                    StudentId = id
                });
            }
        }

    }
}
