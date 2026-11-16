using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Exam.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure
{
    public class CurrentTenant
    {
        private readonly IHttpContextAccessor _http;
        private readonly ExamDbContext _db;
        private bool _resolved;
        private int _userId;
        private string _role = "";
        private int? _teacherScopeId;
        private string? _studentGroupName;

        public CurrentTenant(IHttpContextAccessor http, ExamDbContext db)
        {
            _http = http;
            _db = db;
        }

        public int UserId
        {
            get { Ensure(); return _userId; }
        }

        public bool IsAdmin
        {
            get { Ensure(); return string.Equals(_role, "Admin", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsTeacher
        {
            get { Ensure(); return string.Equals(_role, "Teacher", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsStudent
        {
            get { Ensure(); return string.Equals(_role, "Student", StringComparison.OrdinalIgnoreCase); }
        }

        public int? TeacherScopeId
        {
            get { Ensure(); return _teacherScopeId; }
        }

        public string? StudentGroupName
        {
            get { Ensure(); return _studentGroupName; }
        }

        public bool CanAccessExam(Exam.Core.Domain.Exam? exam)
        {
            if (exam == null) return false;
            if (IsAdmin) return true;
            if (TeacherScopeId.HasValue && exam.TeacherId == TeacherScopeId) return true;
            return IsTeacher && UserId > 0 && exam.TeacherId == UserId;
        }

        public bool CanViewExam(Exam.Core.Domain.Exam? exam)
        {
            if (exam == null) return false;
            if (IsAdmin) return true;
            if (IsTeacher && UserId > 0 && exam.TeacherId == UserId) return true;
            if (IsTeacher && TeacherScopeId.HasValue && exam.TeacherId == TeacherScopeId) return true;
            if (exam.IsPublic && exam.Status != Core.Enums.ExamStatus.Draft) return true;
            if (!IsStudent) return false;
            if (exam.Status == Core.Enums.ExamStatus.Draft) return false;
            return IsAssignedToStudent(exam);
        }

        public IQueryable<Exam.Core.Domain.Exam> VisibleExams()
        {
            Ensure();
            var query = _db.Exams.AsQueryable();
            if (IsAdmin) return query;
            if (IsTeacher && _teacherScopeId.HasValue)
                return query.Where(e => e.TeacherId == _teacherScopeId);

            var publicExams = query.Where(e => e.IsPublic && e.Status != Core.Enums.ExamStatus.Draft);
            if (!IsStudent || _userId <= 0)
                return publicExams;

            var teacherId = _teacherScopeId;
            var groupName = (_studentGroupName ?? string.Empty).Trim().ToLower();
            var userId = _userId;
            return query.Where(e =>
                e.Status != Core.Enums.ExamStatus.Draft && (
                    e.IsPublic
                    || _db.ExamAssignees.Any(a => a.ExamId == e.Id && a.StudentId == userId)
                    || (teacherId.HasValue && e.TeacherId == teacherId && (
                        e.Visibility == null
                        || e.Visibility == ""
                        || e.Visibility.ToLower() == ExamSharing.Private
                        || (e.Visibility.ToLower() == ExamSharing.Group
                            && groupName != ""
                            && e.ShareGroupName != null
                            && e.ShareGroupName.ToLower() == groupName)
                        || (e.Visibility.ToLower() == ExamSharing.Students
                            && _db.ExamAssignees.Any(a => a.ExamId == e.Id && a.StudentId == userId))
                    ))
                ));
        }

        private bool IsAssignedToStudent(Exam.Core.Domain.Exam exam)
        {
            if (exam.IsPublic && exam.Status != Core.Enums.ExamStatus.Draft) return true;
            if (_db.ExamAssignees.Any(a => a.ExamId == exam.Id && a.StudentId == UserId))
                return true;
            if (!TeacherScopeId.HasValue || exam.TeacherId != TeacherScopeId)
                return false;
            var vis = ExamSharing.VisibilityOf(exam.IsPublic, exam.Visibility);
            if (vis == ExamSharing.Public) return exam.IsPublic;
            if (vis == ExamSharing.Group)
            {
                var group = (StudentGroupName ?? string.Empty).Trim();
                return group.Length > 0
                    && !string.IsNullOrWhiteSpace(exam.ShareGroupName)
                    && string.Equals(exam.ShareGroupName, group, StringComparison.OrdinalIgnoreCase);
            }
            if (vis == ExamSharing.Students)
                return _db.ExamAssignees.Any(a => a.ExamId == exam.Id && a.StudentId == UserId);
            return true;
        }

        private void Ensure()
        {
            if (_resolved) return;
            _resolved = true;
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return;

            var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? user.FindFirstValue("sub");
            int.TryParse(raw, out _userId);
            if (RoleClaims.IsAdmin(user)) _role = "Admin";
            else if (RoleClaims.IsTeacher(user)) _role = "Teacher";
            else if (user.IsInRole("Student") || string.Equals(user.FindFirstValue(ClaimTypes.Role), "Student", StringComparison.OrdinalIgnoreCase))
                _role = "Student";
            else
                _role = user.FindFirstValue(ClaimTypes.Role)
                        ?? user.FindFirstValue("role")
                        ?? "";

            var scopeRaw = user.FindFirstValue("teacherScope");
            if (int.TryParse(scopeRaw, out var scope) && scope > 0)
            {
                _teacherScopeId = scope;
            }
            else if (IsTeacher)
            {
                _teacherScopeId = _userId;
            }

            if (IsStudent && _userId > 0)
            {
                var row = _db.Users.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(u => u.Id == _userId)
                    .Select(u => new { u.TeacherId, u.GroupName })
                    .FirstOrDefault();
                if (!_teacherScopeId.HasValue)
                    _teacherScopeId = row?.TeacherId;
                _studentGroupName = row?.GroupName;
            }
        }
    }
}
