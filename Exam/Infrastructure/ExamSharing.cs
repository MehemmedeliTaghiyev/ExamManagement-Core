using Exam.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure
{
    public static class ExamSharing
    {
        public const string Public = "public";
        public const string Private = "private";
        public const string Group = "group";
        public const string Students = "students";

        public static (bool IsPublic, string Visibility) Normalize(bool? isPublic, string? visibility)
        {
            var vis = (visibility ?? string.Empty).Trim().ToLowerInvariant();
            if (vis is "public" or "hamı" or "hami" or "open" or "ictimai")
                return (true, Public);
            if (vis is "group" or "qrup")
                return (false, Group);
            if (vis is "students" or "student" or "tələbə" or "telebe" or "şagird" or "sagird")
                return (false, Students);
            if (vis is "private" or "özəl" or "ozel" or "ozəl")
                return (false, Private);
            if (isPublic == true)
                return (true, Public);
            if (isPublic == false)
                return (false, vis is Group or Students ? vis : Private);
            return (false, string.IsNullOrEmpty(vis) ? Private : vis);
        }

        public static string VisibilityOf(bool isPublic, string? visibility)
        {
            if (isPublic) return Public;
            var vis = (visibility ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(vis) ? Private : vis;
        }

        public static List<string> DistinctTopics(IEnumerable<string?> topics)
        {
            return topics
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.Trim())
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        public static void EnsurePublicAllowsTopics(bool isPublic, IEnumerable<string?> topics)
        {
            if (!isPublic) return;
            if (DistinctTopics(topics).Count > 1)
                throw new PublicExamRequiresSingleTopicException();
        }

        /// <summary>
        /// One-topic = at most one distinct QuestionBank.MainTopic among exam questions
        /// joined on Questions.SourceQnum = QuestionBank.Qnum (empty topics ignored).
        /// </summary>
        public static async Task<List<string>> TopicsForExamAsync(
            ExamDbContext db,
            int examId,
            CancellationToken cancellationToken = default)
        {
            var rows = await (
                from q in db.Questions.AsNoTracking()
                where q.ExamId == examId && q.SourceQnum != null && q.SourceQnum != ""
                join b in db.QuestionBank.AsNoTracking() on q.SourceQnum equals b.Qnum
                where b.MainTopic != null && b.MainTopic != ""
                select b.MainTopic
            ).ToListAsync(cancellationToken);
            return DistinctTopics(rows);
        }

        public static async Task EnsurePublicSingleTopicAsync(
            ExamDbContext db,
            int examId,
            bool isPublic,
            IEnumerable<string?>? extraTopics = null,
            CancellationToken cancellationToken = default)
        {
            if (!isPublic) return;
            var topics = await TopicsForExamAsync(db, examId, cancellationToken);
            if (extraTopics != null)
                topics = DistinctTopics(topics.Concat(extraTopics));
            EnsurePublicAllowsTopics(true, topics);
        }
    }
}
