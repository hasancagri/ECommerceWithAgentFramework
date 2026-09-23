using Catalog.Api.Import;

namespace Catalog.Api.Tests.Import;

// 083 US1 domain-TDD (İLKE VI): capability token oturumunun saf mantığı — token üretimi, IsUsable
// (expiry + consumed), Consume tek-kullanım.
public class ImportSessionTests
{
    [Fact]
    public void Create_ProducesUsableSessionWithUrlSafeToken()
    {
        var result = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(60));

        result.IsSuccess.ShouldBeTrue();
        var session = result.Data!;
        session.Token.ShouldNotBeNullOrWhiteSpace();
        // base64url: '+' '/' '=' bulunmaz.
        session.Token.ShouldNotContain("+");
        session.Token.ShouldNotContain("/");
        session.Token.ShouldNotContain("=");
        session.IsUsable(DateTimeOffset.UtcNow).ShouldBeTrue();
        session.ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_TwoSessions_HaveDistinctTokens()
    {
        var a = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(60)).Data!;
        var b = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(60)).Data!;

        a.Token.ShouldNotBe(b.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveLifetime_ReturnsError(int minutes)
    {
        var result = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(minutes));

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void IsUsable_AfterExpiry_ReturnsFalse()
    {
        var session = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(60)).Data!;

        session.IsUsable(DateTimeOffset.UtcNow.AddMinutes(61)).ShouldBeFalse();
    }

    [Fact]
    public void Consume_MarksConsumedAndStoresRowCount()
    {
        var session = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(60)).Data!;
        var now = DateTimeOffset.UtcNow;

        var result = session.Consume(rowCount: 19711, now);

        result.IsSuccess.ShouldBeTrue();
        session.ConsumedAt.ShouldBe(now);
        session.RowCount.ShouldBe(19711);
        session.IsUsable(now).ShouldBeFalse();
    }

    [Fact]
    public void Consume_SecondTime_ReturnsError()
    {
        var session = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(60)).Data!;
        var now = DateTimeOffset.UtcNow;
        session.Consume(rowCount: 10, now).IsSuccess.ShouldBeTrue();

        var second = session.Consume(rowCount: 5, now);

        second.IsSuccess.ShouldBeFalse();
        session.RowCount.ShouldBe(10); // ilk consume korunur
    }

    [Fact]
    public void Consume_ExpiredSession_ReturnsError()
    {
        var session = ImportSession.Create(Guid.NewGuid(), TimeSpan.FromMinutes(60)).Data!;

        var result = session.Consume(rowCount: 10, DateTimeOffset.UtcNow.AddMinutes(61));

        result.IsSuccess.ShouldBeFalse();
        session.ConsumedAt.ShouldBeNull();
    }
}
