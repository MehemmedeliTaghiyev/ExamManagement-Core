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

        public bool CanAccessExam(Exam.Core.Domain.Exam? exam)
        {
            if (exam == null) return false;
            if (IsAdmin) return true;
            if (TeacherScopeId.HasValue && exam.TeacherId == TeacherScopeId) return true;
            return IsTeacher && UserId > 0 && exam.TeacherId == UserId;
        }

        public IQueryable<Exam.Core.Domain.Exam> VisibleExams()
        {
            Ensure();
            var query = _db.Exams.AsQueryable();
            if (IsAdmin) return query;
            if (!_teacherScopeId.HasValue) return query.Where(_ => false);
            return query.Where(e => e.TeacherId == _teacherScopeId);
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
                return;
            }

            if (IsTeacher)
            {
                _teacherScopeId = _userId;
                return;
            }

            if (IsStudent && _userId > 0)
            {
                var teacherId = _db.Users.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(u => u.Id == _userId)
                    .Select(u => u.TeacherId)
                    .FirstOrDefault();
                _teacherScopeId = teacherId;
            }
        }
    }
}
