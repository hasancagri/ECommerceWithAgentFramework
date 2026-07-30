using System.Reflection;

namespace Customer.Api.Tests;

public class WalletTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);

    private static SavedCard FutureCard(string? label = null) =>
        SavedCard.Create("tok_1", "Visa", "1111", 12, 2030, label);

    [Fact]
    public void Create_SetsUserId_AndEmptyWallet()
    {
        var userId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);

        wallet.UserId.ShouldBe(userId);
        wallet.Cards.ShouldBeEmpty();
    }

    [Fact]
    public void AddCard_AddsCard()
    {
        var wallet = Wallet.Create(Guid.NewGuid());

        wallet.AddCard(FutureCard(), Now).IsSuccess.ShouldBeTrue();

        wallet.Cards.Count.ShouldBe(1);
        wallet.Cards[0].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void AddCard_Rejects_ExpiredCard()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        var expired = SavedCard.Create("tok_x", "Visa", "1111", 1, 2020, null);

        var result = wallet.AddCard(expired, Now);

        result.IsSuccess.ShouldBeFalse();
        wallet.Cards.ShouldBeEmpty();
    }

    [Fact]
    public void SetDefaultCard_KeepsAtMostOneDefault()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddCard(FutureCard("a"), Now);
        wallet.AddCard(FutureCard("b"), Now);
        var c1 = wallet.Cards[0].Id;
        var c2 = wallet.Cards[1].Id;

        wallet.SetDefaultCard(c1).IsSuccess.ShouldBeTrue();
        wallet.Cards.Count(x => x.IsDefault).ShouldBe(1);
        wallet.Cards.Single(x => x.IsDefault).Id.ShouldBe(c1);

        wallet.SetDefaultCard(c2).IsSuccess.ShouldBeTrue();
        wallet.Cards.Count(x => x.IsDefault).ShouldBe(1);
        wallet.Cards.Single(x => x.IsDefault).Id.ShouldBe(c2);
    }

    [Fact]
    public void SetDefaultCard_NotFound_WhenMissing()
    {
        var wallet = Wallet.Create(Guid.NewGuid());

        wallet.SetDefaultCard(Guid.NewGuid()).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void RemoveCard_RemovesAndReturnsToken()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.AddCard(FutureCard(), Now);
        var id = wallet.Cards[0].Id;

        var result = wallet.RemoveCard(id);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Token.ShouldBe("tok_1");
        wallet.Cards.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveCard_NotFound_WhenMissing()
    {
        var wallet = Wallet.Create(Guid.NewGuid());

        wallet.RemoveCard(Guid.NewGuid()).IsSuccess.ShouldBeFalse();
    }

    // INV-3 (PCI): SavedCard hicbir zaman ham PAN/CVV alani icermez — tip duzeyinde yasak.
    [Fact]
    public void SavedCard_HasNo_RawPanOrCvv_Fields()
    {
        var forbidden = new[] { "pan", "cvv", "cardnumber", "securitycode", "cvc", "cvv2" };

        var members = typeof(SavedCard)
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .Concat(typeof(SavedCard)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(f => f.Name.ToLowerInvariant()))
            .ToList();

        foreach (var name in members)
            forbidden.ShouldNotContain(f => name.Contains(f));
    }
}