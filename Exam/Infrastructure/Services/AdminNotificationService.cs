using Exam.Core.Domain;
using Exam.Core.Interfaces;

namespace Exam.Infrastructure.Services
{
    public class AdminNotificationService : IAdminNotificationService
    {
        private readonly IEmailSender _email;

        public AdminNotificationService(IEmailSender email)
        {
            _email = email;
        }

        public Task NotifyTeacherRegisteredAsync(User teacher, CancellationToken cancellationToken = default)
        {
            var (first, last) = Names(teacher);
            var body =
                $"Yeni müəllim qeydə alındı.\n\n" +
                $"Ad: {first}\n" +
                $"Soyad: {last}\n" +
                $"E-poçt: {teacher.Email}\n" +
                $"Əlaqə nömrəsi: {teacher.Phone ?? "—"}\n" +
                $"Qeydiyyat tarixi: {teacher.CreatedAt:yyyy-MM-dd HH:mm} UTC\n" +
                $"Pulsuz sınaq bitir: {(teacher.TrialEndsAt.HasValue ? teacher.TrialEndsAt.Value.ToString("yyyy-MM-dd HH:mm") + " UTC" : "təyin olunmayıb")}";
            return _email.SendAsync("ExamPulse: yeni müəllim", body, cancellationToken);
        }

        public Task NotifyTrialEndedAsync(User teacher, CancellationToken cancellationToken = default)
        {
            var (first, last) = Names(teacher);
            var custom = string.IsNullOrWhiteSpace(teacher.TrialMessage)
                ? "Free trial bitdi."
                : teacher.TrialMessage.Trim();
            var body =
                $"{custom}\n\n" +
                $"Ad: {first}\n" +
                $"Soyad: {last}\n" +
                $"E-poçt: {teacher.Email}\n" +
                $"Əlaqə nömrəsi: {teacher.Phone ?? "—"}\n" +
                $"Sınaq bitmə vaxtı: {(teacher.TrialEndsAt.HasValue ? teacher.TrialEndsAt.Value.ToString("yyyy-MM-dd HH:mm") + " UTC" : "—")}";
            return _email.SendAsync("ExamPulse: free trial bitdi", body, cancellationToken);
        }

        private static (string first, string last) Names(User teacher)
        {
            var first = teacher.FirstName;
            var last = teacher.LastName;
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
            {
                var parts = (teacher.FullName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                first = string.IsNullOrWhiteSpace(first) ? (parts.Length > 0 ? parts[0] : "—") : first;
                last = string.IsNullOrWhiteSpace(last) ? (parts.Length > 1 ? parts[1] : "—") : last;
            }
            return (first, last);
        }
    }
}
