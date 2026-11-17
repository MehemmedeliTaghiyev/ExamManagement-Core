using System.Security.Claims;
using Exam.Core.Domain;
using Exam.Core.DTOs.QuestionBank;
using Exam.Core.Enums;
using Exam.Infrastructure;
using Exam.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Exam.Tests;

public class QuestionBankUnusedFirstTests
{
    [Fact]
    public async Task CopyFromBank_prefers_unused_qnum_then_bumps_use_count()
    {
        await using var db = CreateDb();
        db.Subjects.Add(new Subject { Id = 1, Name = "Riyaziyyat" });
        db.Exams.Add(new Exam.Core.Domain.Exam
        {
            Id = 10,
            SubjectId = 1,
            Title = "Teacher sınaq",
            DurationMinutes = 40,
            TotalQuestions = 1,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamStatus.Live,
            TeacherId = 1
        });
        db.QuestionBank.AddRange(
            Bank(1, "USED", use: 4, last: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Bank(2, "FRESH", use: 0));
        db.Questions.Add(new Question
        {
            ExamId = 99,
            Text = "old",
            SourceQnum = "USED"
        });
        await db.SaveChangesAsync();

        var service = new QuestionBankService(db, AdminTenant(db));
        var result = await service.CopyFromBankAsync(10, new FromBankRequestDto { Count = 1 });

        Assert.Equal(1, result.Added);
        Assert.Equal(new[] { "FRESH" }, result.CopiedQnums);

        var fresh = await db.QuestionBank.SingleAsync(q => q.Qnum == "FRESH");
        Assert.Equal(1, fresh.UseCount);
        Assert.NotNull(fresh.LastUsedAt);
        var used = await db.QuestionBank.SingleAsync(q => q.Qnum == "USED");
        Assert.Equal(4, used.UseCount);
    }

    [Fact]
    public async Task CopyBankFields_does_not_reset_use_counters()
    {
        await using var db = CreateDb();
        db.QuestionBank.Add(Bank(1, "KEEP", use: 7, last: new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var csv = """
Qnum,Text,Type,InputKind,CorrectText,DifficultyLevel,Points,Grade,Subject,Audience,MainTopic,Language,SituationId,SituationTitle,FigureUrl,A,B,C,D,E,Correct
KEEP,yeni mətn,SingleChoice,Choice,A,asan,1,9,Riyaziyyat,teacher,Üçbucaqlar,az,,,,,A,B,C,D,E,A
""";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var service = new QuestionBankService(db, AdminTenant(db));
        var imported = await service.ImportCsvAsync(stream);

        Assert.Equal(1, imported.Updated);
        var row = await db.QuestionBank.SingleAsync(q => q.Qnum == "KEEP");
        Assert.Equal("yeni mətn", row.Text);
        Assert.Equal(7, row.UseCount);
        Assert.Equal(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), row.LastUsedAt);
    }

    private static ExamDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ExamDbContext(options);
    }

    private static CurrentTenant AdminTenant(ExamDbContext db)
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Role, "Admin")
                    },
                    "test"))
            }
        };
        return new CurrentTenant(http, db);
    }

    private static QuestionBank Bank(int id, string qnum, int use, DateTime? last = null) => new()
    {
        Id = id,
        Qnum = qnum,
        Text = qnum,
        Type = QuestionType.SingleChoice,
        InputKind = "Choice",
        Points = 1,
        Audience = "teacher",
        UseCount = use,
        LastUsedAt = last
    };
}
