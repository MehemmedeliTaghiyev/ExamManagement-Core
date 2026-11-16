using Exam.Core.Domain;
using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Exam.Infrastructure;
using Exam.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Exam.Tests;

public class UserAdminServiceTeacherDirectoryTests
{
    [Fact]
    public async Task Directory_includes_disabled_teachers_and_omits_deleted()
    {
        await using var db = CreateDb();
        db.Users.AddRange(
            Teacher(1, "Aygün Müəllim", enabled: true, deleted: false, "Bakı", "Riyaziyyat"),
            Teacher(2, "Elvin Müəllim", enabled: false, deleted: false, "Gəncə", "Fizika"),
            Teacher(3, "Silinmiş", enabled: false, deleted: true, "Şəki", "Kimya"),
            new User
            {
                Id = 10,
                FullName = "Tələbə",
                Email = "student@test.local",
                PasswordHash = "x",
                Role = UserRole.Student,
                IsAccessEnabled = true
            },
            new User
            {
                Id = 11,
                FullName = "Admin",
                Email = "admin@test.local",
                PasswordHash = "x",
                Role = UserRole.Admin,
                IsAccessEnabled = true
            });
        await db.SaveChangesAsync();

        var service = new UserAdminService(db, new NoopNotify());
        var rows = await service.GetTeacherDirectoryAsync();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, t => t.Id == 1 && t.FullName == "Aygün Müəllim"
            && t.Position == "Bakı" && t.Region == "Bakı"
            && t.TeachingSubject == "Riyaziyyat" && t.Subject == "Riyaziyyat");
        Assert.Contains(rows, t => t.Id == 2 && t.FullName == "Elvin Müəllim");
        Assert.DoesNotContain(rows, t => t.Id == 3);
        Assert.DoesNotContain(rows, t => t.Id == 10 || t.Id == 11);
        Assert.All(rows, t =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(t);
            Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static ExamDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ExamDbContext(options);
    }

    private static User Teacher(int id, string name, bool enabled, bool deleted, string region, string subject) => new()
    {
        Id = id,
        FullName = name,
        Email = $"t{id}@secret.local",
        Phone = "0500000000",
        PasswordHash = "hash",
        Role = UserRole.Teacher,
        IsAccessEnabled = enabled,
        IsDeleted = deleted,
        DeletedAt = deleted ? DateTime.UtcNow : null,
        Position = region,
        TeachingSubject = subject
    };

    private sealed class NoopNotify : IAdminNotificationService
    {
        public Task NotifyTeacherRegisteredAsync(User teacher, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyTrialEndedAsync(User teacher, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
