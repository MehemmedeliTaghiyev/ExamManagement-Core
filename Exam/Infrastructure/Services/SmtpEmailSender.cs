using System.Net;
using System.Net.Mail;
using Exam.Core.Interfaces;

namespace Exam.Infrastructure.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailSender> _log;
        private readonly IWebHostEnvironment _env;

        public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> log, IWebHostEnvironment env)
        {
            _config = config;
            _log = log;
            _env = env;
        }

        public async Task SendAsync(string subject, string body, CancellationToken cancellationToken = default)
        {
            var to = _config["Email:NotifyTo"] ?? "mehemmedeli.taghiyev@gmail.com";
            var host = _config["Email:Host"];
            var user = _config["Email:User"];
            var password = _config["Email:Password"];
            var from = _config["Email:From"] ?? user ?? to;
            var port = int.TryParse(_config["Email:Port"], out var p) ? p : 587;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                var dir = Path.Combine(_env.ContentRootPath, "wwwroot", "mail-outbox");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(file, $"To: {to}\nSubject: {subject}\n\n{body}", cancellationToken);
                _log.LogWarning("SMTP tənzimlənməyib. Məktub fayla yazıldı: {File}", file);
                return;
            }

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(user, password)
            };
            using var message = new MailMessage(from, to, subject, body);
            await client.SendMailAsync(message, cancellationToken);
        }
    }
}
