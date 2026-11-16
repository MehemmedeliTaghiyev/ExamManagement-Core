using Exam.Core.Exceptions;
using Exam.Infrastructure;

namespace Exam.Tests;

public class ExamSharingTests
{
    [Fact]
    public void DistinctTopics_is_case_insensitive_and_ignores_empty()
    {
        var topics = ExamSharing.DistinctTopics(new[] { "Üçbucaqlar", "üçbucaqlar", " ", null, "Düzbucaqlı" });
        Assert.Equal(2, topics.Count);
        Assert.Contains("Üçbucaqlar", topics);
        Assert.Contains("Düzbucaqlı", topics);
    }

    [Fact]
    public void Public_allows_zero_or_one_topic()
    {
        ExamSharing.EnsurePublicAllowsTopics(true, Array.Empty<string>());
        ExamSharing.EnsurePublicAllowsTopics(true, new[] { "Üçbucaqlar" });
        ExamSharing.EnsurePublicAllowsTopics(false, new[] { "A", "B" });
    }

    [Fact]
    public void Public_rejects_two_topics()
    {
        Assert.Throws<PublicExamRequiresSingleTopicException>(() =>
            ExamSharing.EnsurePublicAllowsTopics(true, new[] { "Üçbucaqlar", "Düzbucaqlı" }));
    }

    [Fact]
    public void Normalize_maps_public_and_group_audience()
    {
        Assert.Equal((true, ExamSharing.Public), ExamSharing.Normalize(true, null));
        Assert.Equal((true, ExamSharing.Public), ExamSharing.Normalize(false, "public"));
        Assert.Equal((false, ExamSharing.Private), ExamSharing.Normalize(false, "private"));
        Assert.Equal((false, ExamSharing.Group), ExamSharing.Normalize(null, "group"));
        Assert.Equal((false, ExamSharing.Students), ExamSharing.Normalize(null, "students"));
    }
}
