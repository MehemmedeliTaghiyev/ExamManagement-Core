using Exam.Core.Interfaces;

namespace Exam.Infrastructure.Storage
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public LocalFileStorage(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _config = config;
        }

        public async Task<StoredFileResult> SaveAsync(
            Stream content,
            string originalFileName,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default)
        {
            var ext = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";

            var safeFolder = string.Join(
                Path.DirectorySeparatorChar,
                (folder ?? "uploads")
                    .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Path.GetFileName)
                    .Where(p => !string.IsNullOrWhiteSpace(p)));

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var physicalDir = Path.Combine(webRoot, "uploads", safeFolder);
            Directory.CreateDirectory(physicalDir);

            var physicalPath = Path.Combine(physicalDir, fileName);
            await using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await content.CopyToAsync(fs, cancellationToken);
            }

            var key = $"/uploads/{safeFolder.Replace('\\', '/')}/{fileName}";
            return new StoredFileResult(key, ToPublicUrl(key)!);
        }

        public Task DeleteAsync(string? key, CancellationToken cancellationToken = default)
        {
            var physical = ToPhysicalPath(key);
            if (physical != null && File.Exists(physical))
            {
                File.Delete(physical);
            }

            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string? key, CancellationToken cancellationToken = default)
        {
            var physical = ToPhysicalPath(key);
            if (physical == null || !System.IO.File.Exists(physical))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(physical, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(stream);
        }

        public string? ToPublicUrl(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (key.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }

            var path = key.StartsWith('/') ? key : "/" + key;
            var baseUrl = _config["FileStorage:PublicBaseUrl"]?.TrimEnd('/');
            return string.IsNullOrWhiteSpace(baseUrl) ? path : baseUrl + path;
        }

        private string? ToPhysicalPath(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (key.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = key.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            if (relative.Contains("..", StringComparison.Ordinal)) return null;

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            return Path.Combine(webRoot, relative);
        }
    }
}
