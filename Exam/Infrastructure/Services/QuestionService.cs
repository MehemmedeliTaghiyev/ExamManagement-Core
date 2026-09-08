using Exam.Core.Domain;
using Exam.Core.DTOs.Exam;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

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
            return await _context.Questions
                .Where(q => q.ExamId == examId)
                .Select(q => new QuestionResponseDto
                {
                    Id = q.Id,
                    ExamId = q.ExamId,
                    Text = q.Text,
                    Points = q.Points,
                    Type = q.Type,
                    Options = _context.QuestionOptions
                        .Where(o => o.QuestionId == q.Id)
                        .Select(o => new QuestionOptionResponseDto
                        {
                            Id = o.Id,
                            QuestionId = o.QuestionId,
                            OptionText = o.OptionText,
                            IsCorrect = o.IsCorrect
                        })
                        .ToList() // NO semi-colon here
                })
                .ToListAsync(); // Attach ToListAsync here
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

            // Save child options linked by QuestionId
            if (dto.Options != null && dto.Options.Any())
            {
                var options = dto.Options.Select(o => new QuestionOption
                {
                    QuestionId = question.Id,
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect
                }).ToList();

                _context.QuestionOptions.AddRange(options);
                await _context.SaveChangesAsync();
            }

            return question;
        }

    }
}
