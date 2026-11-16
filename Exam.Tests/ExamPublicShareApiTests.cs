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

public class ExamPublicShareApiTests : IClassFixture<ExamApiFactory>
{
    private const string Password = "Passw0rd!";
    private readonly ExamApiFactory _factory;
    private readonly HttpClient _client;
    private readonly Ids _ids;

    public ExamPublicShareApiTests(ExamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _ids = Seed();
    }

    [Fact]
    public async Task Student_list_includes_other_teacher_public_plus_own_private()
    {
        var token = await LoginAsync("student-a@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/Exams?pageSize=50");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var items = await ReadItemsAsync(res);
        var ids = items.Select(IdOf).ToHashSet();
        Assert.Contains(_ids.PublicExam, ids);
        Assert.Contains(_ids.PrivateExam, ids);
        Assert.DoesNotContain(_ids.OtherPrivate, ids);
        Assert.DoesNotContain(_ids.DraftPublic, ids);

        var pub = items.First(e => IdOf(e) == _ids.PublicExam);
        Assert.True(pub.GetProperty("isPublic").GetBoolean());
        Assert.Equal("public", pub.GetProperty("visibility").GetString());
        Assert.Equal("Üçbucaqlar", pub.GetProperty("topic").GetString());
        Assert.Equal(_ids.TeacherA, pub.GetProperty("teacherId").GetInt32());
    }

    [Fact]
    public async Task TeacherId_and_isPublic_filter_public_directory()
    {
        var token = await LoginAsync("student-b@test.local");
        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/Exams?teacherId={_ids.TeacherA}&isPublic=true");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var items = await ReadItemsAsync(res);
        AssertPublicDirectory(items);
    }

    [Fact]
    public async Task Anonymous_public_catalog_by_teacher_is_200()
    {
        using var res = await _client.GetAsync($"/api/Exams/public?teacherId={_ids.TeacherA}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var items = await ReadItemsAsync(res);
        AssertPublicDirectory(items);
    }

    [Fact]
    public async Task Multi_topic_exam_cannot_be_made_public()
    {
        var token = await LoginAsync("teacher-a@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/Exams/{_ids.MultiTopicExam}/share");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new { isPublic = true, visibility = "public" });
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("PUBLIC_MULTI_TOPIC", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Teacher_can_share_single_topic_exam_public()
    {
        var token = await LoginAsync("teacher-a@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/Exams/{_ids.ShareableExam}/share");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new { isPublic = true, visibility = "public" });
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var body = doc.RootElement;
        Assert.True(body.GetProperty("isPublic").GetBoolean());
        Assert.Equal("public", body.GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task Group_share_is_visible_only_to_that_group()
    {
        var grouped = await LoginAsync("student-group@test.local");
        using (var req = new HttpRequestMessage(HttpMethod.Get, "/api/Exams?pageSize=50"))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", grouped);
            using var res = await _client.SendAsync(req);
            var ids = (await ReadItemsAsync(res)).Select(IdOf).ToHashSet();
            Assert.Contains(_ids.GroupExam, ids);
        }

        var other = await LoginAsync("student-a@test.local");
        using (var req = new HttpRequestMessage(HttpMethod.Get, "/api/Exams?pageSize=50"))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", other);
            using var res = await _client.SendAsync(req);
            var ids = (await ReadItemsAsync(res)).Select(IdOf).ToHashSet();
            Assert.DoesNotContain(_ids.GroupExam, ids);
        }
    }

    [Fact]
    public async Task Student_gets_teacher_profile_not_403()
    {
        var token = await LoginAsync("student-b@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/Users/{_ids.TeacherA}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(_ids.TeacherA, doc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Aygün Müəllim", doc.RootElement.GetProperty("fullName").GetString());
        Assert.False(doc.RootElement.TryGetProperty("email", out _));
        Assert.False(doc.RootElement.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Student_cannot_read_another_student_by_id()
    {
        var token = await LoginAsync("student-b@test.local");
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/Users/{_ids.StudentA}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private async Task<string> LoginAsync(string email)
    {
        using var res = await _client.PostAsJsonAsync("/api/Auth/login", new { email, password = Password });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()
               ?? throw new InvalidOperationException("login returned no token");
    }

    private static async Task<List<JsonElement>> ReadItemsAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(e => e.Clone()).ToList();
        if (root.TryGetProperty("items", out var items) || root.TryGetProperty("Items", out items))
            return items.EnumerateArray().Select(e => e.Clone()).ToList();
        throw new InvalidOperationException("exam list was not an array or paged items");
    }

    private static int IdOf(JsonElement e) => e.GetProperty("id").GetInt32();

    private void AssertPublicDirectory(List<JsonElement> items)
    {
        var ids = items.Select(IdOf).ToHashSet();
        Assert.Contains(_ids.PublicExam, ids);
        Assert.DoesNotContain(_ids.PrivateExam, ids);
        Assert.DoesNotContain(_ids.OtherPrivate, ids);
        Assert.DoesNotContain(_ids.DraftPublic, ids);
        Assert.DoesNotContain(_ids.GroupExam, ids);
        Assert.All(items, e =>
        {
            Assert.True(e.GetProperty("isPublic").GetBoolean());
            Assert.Equal(_ids.TeacherA, e.GetProperty("teacherId").GetInt32());
        });
    }

    private Ids Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
        if (db.Users.IgnoreQueryFilters().Any(u => u.Email == "teacher-a@test.local"))
        {
            return LoadIds(db);
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(Password);
        var teacherA = new User
        {
            FullName = "Aygün Müəllim",
            Email = "teacher-a@test.local",
            PasswordHash = hash,
            Role = UserRole.Teacher,
            IsAccessEnabled = true,
            Position = "Bakı",
            TeachingSubject = "Riyaziyyat"
        };
        var teacherB = new User
        {
            FullName = "Elvin Müəllim",
            Email = "teacher-b@test.local",
            PasswordHash = hash,
            Role = UserRole.Teacher,
            IsAccessEnabled = true
        };
        db.Users.AddRange(teacherA, teacherB);
        db.SaveChanges();

        var studentA = new User
        {
            FullName = "Tələbə A",
            Email = "student-a@test.local",
            PasswordHash = hash,
            Role = UserRole.Student,
            TeacherId = teacherA.Id,
            IsAccessEnabled = true
        };
        var studentGroup = new User
        {
            FullName = "Tələbə Qrup",
            Email = "student-group@test.local",
            PasswordHash = hash,
            Role = UserRole.Student,
            TeacherId = teacherA.Id,
            GroupName = "11A",
            IsAccessEnabled = true
        };
        var studentB = new User
        {
            FullName = "Tələbə B",
            Email = "student-b@test.local",
            PasswordHash = hash,
            Role = UserRole.Student,
            TeacherId = teacherB.Id,
            IsAccessEnabled = true
        };
        db.Users.AddRange(studentA, studentGroup, studentB);

        var subject = new Subject { Name = "Riyaziyyat" };
        db.Subjects.Add(subject);
        db.QuestionBank.AddRange(
            new QuestionBank { Qnum = "T1", Text = "üçbucaq 1", MainTopic = "Üçbucaqlar", Audience = "teacher" },
            new QuestionBank { Qnum = "T2", Text = "üçbucaq 2", MainTopic = "Üçbucaqlar", Audience = "teacher" },
            new QuestionBank { Qnum = "D1", Text = "düzbucaq 1", MainTopic = "Düzbucaqlı", Audience = "teacher" });
        db.SaveChanges();

        var now = DateTime.UtcNow;
        var pub = LiveExam(subject.Id, teacherA.Id, "Açıq üçbucaq", now, isPublic: true, topic: "Üçbucaqlar");
        var priv = LiveExam(subject.Id, teacherA.Id, "Özəl sınaq", now, isPublic: false);
        var otherPriv = LiveExam(subject.Id, teacherB.Id, "Elvin özəl", now, isPublic: false);
        var draft = LiveExam(subject.Id, teacherA.Id, "Qaralama açıq", now, isPublic: true, topic: "Üçbucaqlar");
        draft.Status = ExamStatus.Draft;
        var multi = LiveExam(subject.Id, teacherA.Id, "Qarışıq", now, isPublic: false);
        var shareable = LiveExam(subject.Id, teacherA.Id, "Paylaşılacaq", now, isPublic: false, topic: "Üçbucaqlar");
        var groupExam = LiveExam(subject.Id, teacherA.Id, "11A sınağı", now, isPublic: false);
        groupExam.Visibility = ExamSharing.Group;
        groupExam.ShareGroupName = "11A";
        db.Exams.AddRange(pub, priv, otherPriv, draft, multi, shareable, groupExam);
        db.SaveChanges();

        db.Questions.AddRange(
            Q(pub.Id, "T1"),
            Q(priv.Id, "T1"),
            Q(multi.Id, "T1"),
            Q(multi.Id, "D1"),
            Q(shareable.Id, "T1"),
            Q(shareable.Id, "T2"));
        db.SaveChanges();

        return new Ids(teacherA.Id, teacherB.Id, studentA.Id, pub.Id, priv.Id, otherPriv.Id, draft.Id, multi.Id, shareable.Id, groupExam.Id);
    }

    private static Ids LoadIds(ExamDbContext db)
    {
        var teacherA = db.Users.IgnoreQueryFilters().First(u => u.Email == "teacher-a@test.local").Id;
        var teacherB = db.Users.IgnoreQueryFilters().First(u => u.Email == "teacher-b@test.local").Id;
        var studentA = db.Users.IgnoreQueryFilters().First(u => u.Email == "student-a@test.local").Id;
        int ExamId(string title) => db.Exams.First(e => e.Title == title).Id;
        return new Ids(
            teacherA,
            teacherB,
            studentA,
            ExamId("Açıq üçbucaq"),
            ExamId("Özəl sınaq"),
            ExamId("Elvin özəl"),
            ExamId("Qaralama açıq"),
            ExamId("Qarışıq"),
            ExamId("Paylaşılacaq"),
            ExamId("11A sınağı"));
    }

    private static Exam.Core.Domain.Exam LiveExam(
        int subjectId,
        int teacherId,
        string title,
        DateTime now,
        bool isPublic,
        string? topic = null) => new()
    {
        SubjectId = subjectId,
        TeacherId = teacherId,
        Title = title,
        DurationMinutes = 45,
        TotalQuestions = 2,
        StartTime = now.AddHours(-1),
        EndTime = now.AddHours(2),
        Status = ExamStatus.Live,
        CreatedAt = now,
        IsPublic = isPublic,
        Visibility = isPublic ? ExamSharing.Public : ExamSharing.Private,
        Topic = topic
    };

    private static Question Q(int examId, string qnum) => new()
    {
        ExamId = examId,
        Text = qnum,
        SourceQnum = qnum,
        Points = 1
    };

    private sealed record Ids(
        int TeacherA,
        int TeacherB,
        int StudentA,
        int PublicExam,
        int PrivateExam,
        int OtherPrivate,
        int DraftPublic,
        int MultiTopicExam,
        int ShareableExam,
        int GroupExam);
}
