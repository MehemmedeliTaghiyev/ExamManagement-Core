using Exam.Core.Domain;
using Exam.Core.DTOs.Submission;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

namespace Exam.Infrastructure.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ExamDbContext _context;

        public SubmissionService(ExamDbContext context)
        {
            _context = context;
        }

        // 1. İmtahanı Başlatmaq
        public async Task<int> StartExamAsync(StartExamDto dto)
        {
            // Tələbənin bu imtahanda artıq davam edən və ya bitmiş sessiyası var?
            var existingSubmission = await _context.StudentExams
                .FirstOrDefaultAsync(se => se.StudentId == dto.StudentId && se.ExamId == dto.ExamId);

            if (existingSubmission != null)
            {
                return existingSubmission.Id; // Artıq başlayıbsa, mövcud İD-ni qaytarırıq
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

            return studentExam.Id;
        }

        // 2. Cavabları Təhvil Vermək və Avtomatik Qiymətləndirmək (Auto-Grading)
        public async Task<ExamResultDto?> SubmitExamAsync(SubmitExamDto dto)
        {
            // Naviqasiya xassəsi olmadığı üçün birbaşa İD ilə tapırıq
            var studentExam = await _context.StudentExams
                .FirstOrDefaultAsync(se => se.Id == dto.StudentExamId);

            if (studentExam == null || studentExam.Status == StudentExamStatus.Submitted)
                return null;

            // Sualları və variantları gətiririk
            var questions = await _context.Questions
    .Where(q => q.ExamId == studentExam.ExamId)
    .ToListAsync();

            // 2. Tapılan sualların İD-lərini siyahı olaraq götürürük
            var questionIds = questions.Select(q => q.Id).ToList();

            // 3. Bu suallara aid olan bütün variantları ayrı sorğu ilə çəkirik
            var options = await _context.QuestionOptions
                .Where(o => questionIds.Contains(o.QuestionId))
                .ToListAsync();

            // 4. Variantları QuestionId-yə görə qruplayırıq (LookUp vasitəsilə)
            var optionsByQuestionId = options.ToLookup(o => o.QuestionId);

            int correctCount = 0;
            int totalQuestions = questions.Count;

            foreach (var question in questions)
            {
                if (dto.Answers.TryGetValue(question.Id, out var selectedOptionId))
                {
                    // Naviqasiya xassəsi əvəzinə yaddaşdakı Lookup-dan variantı tapırıq
                    var isCorrect = optionsByQuestionId[question.Id]
                        .Any(o => o.Id == selectedOptionId && o.IsCorrect);

                    if (isCorrect)
                    {
                        correctCount++;
                    }

                    _context.StudentAnswers.Add(new StudentAnswer
                    {
                        StudentExamId = studentExam.Id,
                        QuestionId = question.Id,
                        SelectedOptionId = selectedOptionId
                    });
                }
            }

            int wrongCount = totalQuestions - correctCount;
            decimal scorePercentage = totalQuestions > 0
                ? Math.Round(((decimal)correctCount / totalQuestions) * 100, 2)
                : 0;

            studentExam.SubmittedAt = DateTime.UtcNow;
            studentExam.FinalScore = scorePercentage;
            studentExam.CorrectAnswersCount = correctCount;
            studentExam.WrongAnswersCount = wrongCount;
            studentExam.Status = StudentExamStatus.Submitted;

            await _context.SaveChangesAsync();

            return await GetStudentResultAsync(studentExam.Id);
        }

        // 3. İmtahan Nəticəsini Əldə Etmək
        public async Task<ExamResultDto?> GetStudentResultAsync(int studentExamId)
        {
            return await (from se in _context.StudentExams
                          join exam in _context.Exams on se.ExamId equals exam.Id
                          where se.Id == studentExamId
                          select new ExamResultDto
                          {
                              StudentExamId = se.Id,
                              ExamId = exam.Id,
                              ExamTitle = exam.Title,
                              TotalQuestions = exam.TotalQuestions,
                              CorrectAnswers = se.CorrectAnswersCount,
                              WrongAnswers = se.WrongAnswersCount,
                              ScorePercentage = se.FinalScore,
                              Status = se.Status.ToString(),
                              SubmittedAt = se.SubmittedAt ?? DateTime.UtcNow
                          }).FirstOrDefaultAsync();
        }

        // 4. Tələbənin Bütün İmtahan Tarixçəsi
        public async Task<IReadOnlyList<ExamResultDto>> GetStudentHistoryAsync(int studentId)
        {
            return await (from se in _context.StudentExams
                          join exam in _context.Exams on se.ExamId equals exam.Id
                          where se.StudentId == studentId && se.Status != StudentExamStatus.InProgress
                          select new ExamResultDto
                          {
                              StudentExamId = se.Id,
                              ExamId = exam.Id,
                              ExamTitle = exam.Title,
                              TotalQuestions = exam.TotalQuestions,
                              CorrectAnswers = se.CorrectAnswersCount,
                              WrongAnswers = se.WrongAnswersCount,
                              ScorePercentage = se.FinalScore,
                              Status = se.Status.ToString(),
                              SubmittedAt = se.SubmittedAt ?? DateTime.UtcNow
                          }).ToListAsync();
        }
    }
}
