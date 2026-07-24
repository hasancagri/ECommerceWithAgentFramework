namespace ChatAgent.Tests;

// 009 saf kuralları: başlık türetme (FR-003), bağlam penceresi (FR-005), anonim TTL filtresi (FR-009).
public class ConversationRulesTests
{
    [Fact]
    public void DeriveTitle_EmptyOrWhitespace_FallsBackToDefault()
    {
        ConversationRules.DeriveTitle(null).ShouldBe(ConversationRules.DefaultTitle);
        ConversationRules.DeriveTitle("").ShouldBe(ConversationRules.DefaultTitle);
        ConversationRules.DeriveTitle("   ").ShouldBe(ConversationRules.DefaultTitle);
    }

    [Fact]
    public void DeriveTitle_ShortText_UsedAsIs_NewlinesFlattened()
    {
        ConversationRules.DeriveTitle("iPhone stok var mı?").ShouldBe("iPhone stok var mı?");
        ConversationRules.DeriveTitle("satır\nbaşı").ShouldBe("satır başı");
    }

    [Fact]
    public void DeriveTitle_LongText_TruncatesAtWordBoundary_WithEllipsis()
    {
        var title = ConversationRules.DeriveTitle(
            "Bu çok uzun bir ilk mesaj ve başlık olarak olduğu gibi kullanılamayacak kadar fazla kelime içeriyor");

        title.Length.ShouldBeLessThanOrEqualTo(ConversationRules.MaxTitleLength + 1); // +1 = "…"
        title.ShouldEndWith("…");
        title.ShouldNotContain("  ");
    }

    [Fact]
    public void TakeContextWindow_ShorterThanWindow_ReturnsAll()
    {
        var items = new[] { "a", "b", "c" };

        ConversationRules.TakeContextWindow(items, 10).ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void TakeContextWindow_LongerThanWindow_KeepsLastN_InOrder()
    {
        var items = Enumerable.Range(1, 10).ToList();

        ConversationRules.TakeContextWindow(items, 4).ShouldBe([7, 8, 9, 10]);
    }

    [Fact]
    public void TakeContextWindow_NonPositiveWindow_FallsBackToDefault()
    {
        var items = Enumerable.Range(1, 100).ToList();

        ConversationRules.TakeContextWindow(items, 0)
            .Count.ShouldBe(ConversationRules.DefaultContextWindowItems);
    }

    [Fact]
    public void IsExpiredAnonymous_OwnedConversation_NeverExpires()
    {
        var old = DateTimeOffset.UtcNow.AddYears(-1);

        ConversationRules.IsExpiredAnonymous("user-1", old, DateTimeOffset.UtcNow, TimeSpan.FromHours(24))
            .ShouldBeFalse(); // FR-008: login kullanıcıda TTL yok
    }

    [Fact]
    public void IsExpiredAnonymous_OwnerlessOldConversation_Expires()
    {
        var now = DateTimeOffset.UtcNow;

        ConversationRules.IsExpiredAnonymous(null, now.AddHours(-25), now, TimeSpan.FromHours(24)).ShouldBeTrue();
        ConversationRules.IsExpiredAnonymous(null, now.AddHours(-1), now, TimeSpan.FromHours(24)).ShouldBeFalse();
    }
}