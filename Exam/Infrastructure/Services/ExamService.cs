using Exam.Core.DTOs.Exam;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using PagedResult = Exam.Core.DTOs.Exam.PagedResult<Exam.Core.DTOs.Exam.ExamResponseDto>;

namespace Exam.Infrastructure.Services
{
    public class ExamService : IExamService
    {
        private readonly ExamDbContext _context;

        public ExamService(ExamDbContext context)
        {
            _context = context;
        }

        public async Task<Exam.Core.Domain.Exam?> CreateExamAsync(CreateExamDto dto)
        {
            // 1. Fənnin (Subject) daxil edilən ID ilə varlığını yoxlayırıq
            var subjectExists = await _context.Subjects.AnyAsync(s => s.Id == dto.SubjectId);
            if (!subjectExists)
            {
                return null; // Fənn tapılmadıqda controller-ə null qaytarırıq
            }

            // 2. Yeni Exam obyektini formalaşdırırıq
            var newExam = new Exam.Core.Domain.Exam
            {
                SubjectId = dto.SubjectId,
                Title = dto.Title,
                DurationMinutes = dto.DurationMinutes,
                TotalQuestions = dto.TotalQuestions,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Status = Core.Enums.ExamStatus.Live, // Susmaya görə status
                SubmissionsCount = 0,
                CreatedAt = DateTime.UtcNow // Bazaya düşən mütləq tarix
            };

            // 3. Əlavə edib yadda saxlayırıq
            _context.Exams.Add(newExam);
            await _context.SaveChangesAsync();

            return newExam;
        }

        public async Task<ExamCardDto?> GetExamCardByIdAsync(int examId)
        {
            // Joining Exam with Subject to populate SubjectName
            var query = from exam in _context.Exams
                        where exam.Id == examId
                        join subject in _context.Subjects on exam.SubjectId equals subject.Id
                        select new ExamCardDto
                        {
                            Id = exam.Id,
                            Title = exam.Title,
                            SubjectName = subject.Name,
                            TotalQuestions = exam.TotalQuestions,
                            DurationMinutes = exam.DurationMinutes,
                            SubmissionsCount = exam.SubmissionsCount,
                            StartTime = exam.StartTime,
                            Status = exam.Status.ToString()
                        };

            return await query.FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<ExamCardDto>> GetStudentExamCardsAsync()
        {
            var query = from exam in _context.Exams
                        join subject in _context.Subjects on exam.SubjectId equals subject.Id
                        select new ExamCardDto
                        {
                            Id = exam.Id,
                            Title = exam.Title,
                            SubjectName = subject.Name,
                            TotalQuestions = exam.TotalQuestions,
                            DurationMinutes = exam.DurationMinutes,
                            SubmissionsCount = exam.SubmissionsCount,
                            StartTime = exam.StartTime,
                            Status = exam.Status.ToString()
                        };

            return await query.ToListAsync();
        }

        public async Task<string?> UploadPdfAsync(int examId, IFormFile file)
        {
            // 1. İmtahanın varlığını yoxlayırıq
            var exam = await _context.Exams.FindAsync(examId);
            if (exam == null)
                return null; // Controller-də 404 qaytarmaq üçün null dönürük

            // 2. Qovluğu hazırlayırıq (wwwroot/uploads/pdfs)
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "pdfs");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // 3. Unikal fayl adı yaradıb fiziki diskə yazırıq
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4. DB-də PDF yolunu yeniləyirik
            var relativePath = $"/uploads/pdfs/{uniqueFileName}";
            exam.PdfFilePath = relativePath;
            await _context.SaveChangesAsync();

            return relativePath;
        }

        public async Task<ExamResponseDto?> GetExamByIdAsync(int id)
        {
            return await (from exam in _context.Exams
                          join subject in _context.Subjects on exam.SubjectId equals subject.Id
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
                              StartTime = exam.StartTime
                          }).FirstOrDefaultAsync();
        }

        public async Task<ExamResponseDto?> GetExamResponseByIdAsync(int id)
        {
            return await (from exam in _context.Exams
                          join subject in _context.Subjects on exam.SubjectId equals subject.Id
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
                              Status = exam.Status.ToString(),
                              StartTime = exam.StartTime,
                              EndTime = exam.EndTime,
                              CreatedAt = exam.CreatedAt
                          }).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateExamAsync(int id, UpdateExamDto dto)
        {
            // Find the Exam entity directly from the DbContext
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null)
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
            if (exam == null)
                return false;

            // 2. Əgər imtahana bağlı PDF faylı varsa, fiziki olaraq diskdən silirik
            if (!string.IsNullOrEmpty(exam.PdfFilePath))
            {
                var relativePath = exam.PdfFilePath.TrimStart('/', '\\');
                var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
            }

            // 3. Entity-ni bazadan silirik
            _context.Exams.Remove(exam);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<PagedResult<ExamResponseDto>> GetAllExamsAsync(ExamQueryParameters queryParameters)
        {
            // 1. Əsas sorğunu hazırlayırıq
            var query = from exam in _context.Exams
                        join subject in _context.Subjects on exam.SubjectId equals subject.Id
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

            // 7. Qaydılacaq obyekt
            return new PagedResult<ExamResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = queryParameters.PageNumber,
                PageSize = queryParameters.PageSize
            };
        }
        
    }
}
