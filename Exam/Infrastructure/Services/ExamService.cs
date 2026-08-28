using Exam.Core.DTOs.Exam;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IEnumerable<ExamResponseDto>> GetAllExamsAsync()
        {
            return await (from exam in _context.Exams
                          join subject in _context.Subjects on exam.SubjectId equals subject.Id
                          orderby exam.CreatedAt descending // Ən son yaradılanlar üstdə olsun
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
                          }).ToListAsync();
        }
    }
}
