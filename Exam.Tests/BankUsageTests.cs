using Exam.Core.Domain;
using Exam.Infrastructure.Banking;

namespace Exam.Tests;

public class BankUsageTests
{
    [Fact]
    public void OrderByPreference_unused_then_lowest_use_then_oldest_last_used()
    {
        var copies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["USED-1"] = 1,
            ["USED-3"] = 3
        };
        var unused = Bank("UNUSED", use: 0);
        var lowOld = Bank("USED-1", use: 1, last: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var lowNew = Bank("USED-1B", use: 1, last: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var high = Bank("USED-3", use: 2);
        var ordered = BankUsage.OrderByPreference(
            new[] { high, lowNew, unused, lowOld },
            copies,
            new Random(0));

        Assert.Equal(new[] { "UNUSED", "USED-1", "USED-1B", "USED-3" }, ordered.Select(q => q.Qnum));
        Assert.True(BankUsage.Unused(unused, copies));
        Assert.False(BankUsage.Unused(high, copies));
        Assert.Equal(3, BankUsage.EffectiveUse(high, copies));
    }

    [Fact]
    public void TakePreferred_fills_unused_before_reuse()
    {
        var copies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["OLD"] = 2 };
        var taken = BankUsage.TakePreferred(
            new[] { Bank("OLD", use: 2), Bank("NEW-A"), Bank("NEW-B") },
            need: 2,
            already: Array.Empty<QuestionBank>(),
            copies,
            new Random(1));

        Assert.Equal(2, taken.Count);
        Assert.All(taken, q => Assert.True(BankUsage.Unused(q, copies)));
        Assert.DoesNotContain(taken, q => q.Qnum == "OLD");
    }

    private static QuestionBank Bank(string qnum, int use = 0, DateTime? last = null) => new()
    {
        Id = qnum.GetHashCode(),
        Qnum = qnum,
        Text = qnum,
        UseCount = use,
        LastUsedAt = last
    };
}
