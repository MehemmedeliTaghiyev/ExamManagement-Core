namespace Exam.Core.Interfaces
{
    public record StoredFileResult(string Key, string PublicUrl);

    public interface IFileStorage
    {
        Task<StoredFileResult> SaveAsync(
            Stream content,
            string originalFileName,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(string? key, CancellationToken cancellationToken = default);

        Task<Stream?> OpenReadAsync(string? key, CancellationToken cancellationToken = default);

        string? ToPublicUrl(string? key);
    }
}
