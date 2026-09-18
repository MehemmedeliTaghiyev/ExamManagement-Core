using Exam.Core.Enums;
using Exam.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Services
{
    public class TrialReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<TrialReminderHostedService> _log;

        public TrialReminderHostedService(IServiceScopeFactory scopes, ILogger<TrialReminderHostedService> log)
        {
            _scopes = scopes;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Trial reminder failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task TickAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
            var notify = scope.ServiceProvider.GetRequiredService<IAdminNotificationService>();
            var now = DateTime.UtcNow;
            var due = await db.Users.IgnoreQueryFilters()
                .Where(u => u.Role == UserRole.Teacher
                            && !u.IsDeleted
                            && u.TrialEndsAt != null
                            && u.TrialEndsAt <= now
                            && u.TrialNotifiedAt == null)
                .ToListAsync(stoppingToken);

            foreach (var teacher in due)
            {
                await notify.NotifyTrialEndedAsync(teacher, stoppingToken);
                teacher.TrialNotifiedAt = now;
            }

            if (due.Count > 0)
            {
                await db.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
