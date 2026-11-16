using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Exam.Core.Domain;
using Exam.Core.Enums;
using Exam.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exam.Tests;

public class UsersTeacherDirectoryApiTests : IClassFixture<ExamApiFactory>
{
    private const string Password = "Passw0rd!";
    private readonly ExamApiFactory _factory;
    private readonly HttpClient _client;

    public UsersTeacherDirectoryApiTests(ExamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        Seed();
    }

    [Fact]
    public async Task Student_jwt_gets_public_teacher_directory_not_403()
    {
        var token = await LoginAsync("student@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/Users?role=Teacher&includeDeleted=false");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var rows = await ReadArrayAsync(res);
        Assert.Equal(2, rows.GetArrayLength());
        var names = rows.EnumerateArray().Select(r => r.GetProperty("fullName").GetString()).ToList();
        Assert.Contains("Aygün Müəllim", names);
        Assert.Contains("Elvin Müəllim", names);
        Assert.DoesNotContain("Silinmiş Müəllim", names);

        foreach (var row in rows.EnumerateArray())
        {
            Assert.True(row.TryGetProperty("id", out _));
            Assert.True(row.TryGetProperty("fullName", out _));
            Assert.False(row.TryGetProperty("email", out _));
            Assert.False(row.TryGetProperty("passwordHash", out _));
            Assert.False(row.TryGetProperty("phone", out _));
            Assert.False(row.TryGetProperty("token", out _));
        }

        var elvin = rows.EnumerateArray().First(r => r.GetProperty("fullName").GetString() == "Elvin Müəllim");
        Assert.Equal("Gəncə", elvin.GetProperty("position").GetString());
        Assert.Equal("Gəncə", elvin.GetProperty("region").GetString());
        Assert.Equal("Fizika", elvin.GetProperty("teachingSubject").GetString());
        Assert.Equal("Fizika", elvin.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Anonymous_teacher_directory_is_200_not_401()
    {
        using var res = await _client.GetAsync("/api/Users?role=Teacher&includeDeleted=false");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var rows = await ReadArrayAsync(res);
        Assert.Equal(2, rows.GetArrayLength());
    }

    [Fact]
    public async Task Student_cannot_list_students()
    {
        var token = await LoginAsync("student@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/Users?role=Student");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_student_list_stays_401()
    {
        using var res = await _client.GetAsync("/api/Users?role=Student");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Teacher_jwt_still_lists_own_students_not_other_teachers()
    {
        var token = await LoginAsync("teacher@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/Users?role=Teacher&includeDeleted=false");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var rows = await ReadArrayAsync(res);
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("Tələbə One", rows[0].GetProperty("fullName").GetString());
        Assert.Equal("student@test.local", rows[0].GetProperty("email").GetString());
    }

    private async Task<string> LoginAsync(string email)
    {
        using var res = await _client.PostAsJsonAsync("/api/Auth/login", new { email, password = Password });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()
               ?? throw new InvalidOperationException("login returned no token");
    }

    private static async Task<JsonElement> ReadArrayAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        return doc.RootElement.Clone();
    }

    private void Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
        if (db.Users.IgnoreQueryFilters().Any()) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(Password);
        var teacher = new User
        {
            FullName = "Aygün Müəllim",
            Email = "teacher@test.local",
            PasswordHash = hash,
            Role = UserRole.Teacher,
            IsAccessEnabled = true,
            Position = "Bakı",
            TeachingSubject = "Riyaziyyat"
        };
        db.Users.Add(teacher);
        db.SaveChanges();

        db.Users.AddRange(
            new User
            {
                FullName = "Elvin Müəllim",
                Email = "elvin@secret.local",
                PasswordHash = hash,
                Role = UserRole.Teacher,
                IsAccessEnabled = false,
                Position = "Gəncə",
                TeachingSubject = "Fizika"
            },
            new User
            {
                FullName = "Silinmiş Müəllim",
                Email = "gone@secret.local",
                PasswordHash = hash,
                Role = UserRole.Teacher,
                IsAccessEnabled = false,
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                Position = "Şəki",
                TeachingSubject = "Kimya"
            },
            new User
            {
                FullName = "Tələbə One",
                Email = "student@test.local",
                PasswordHash = hash,
                Role = UserRole.Student,
                TeacherId = teacher.Id,
                IsAccessEnabled = true
            });
        db.SaveChanges();
    }
}
