using Exam.Core.Domain;

namespace Exam.Core.Interfaces
{
    public interface IAdminNotificationService
    {
        Task NotifyTeacherRegisteredAsync(User teacher, CancellationToken cancellationToken = default);
        Task NotifyTrialEndedAsync(User teacher, CancellationToken cancellationToken = default);
    }
}
