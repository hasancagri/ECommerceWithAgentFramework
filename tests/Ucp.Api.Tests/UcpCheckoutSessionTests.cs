using Shouldly;
using Ucp.Api.Domains.Sessions;
using Ucp.Api.Domains.Sessions.ValueObjects;
using Xunit;

namespace Ucp.Api.Tests;

// 072 İlke VI: UcpCheckoutSession durum makinesi + invariant'ları test-first.
public class UcpCheckoutSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<UcpLink> Links() => [UcpLink.Create("tos", "https://s/tos").Data!];
    private static IReadOnlyList<UcpLineItem> Items(long price = 5000, int qty = 2)
        => [UcpLineItem.Create("11111111-1111-1111-1111-111111111111", "Kitap", price, qty).Data!];
    private static UcpBuyer Buyer() => UcpBuyer.Create("a@b.com", "Ada", "Lovelace").Data!;
    private static UcpFulfillment Shipping() => UcpFulfillment.Create("shipping", "standard", "Standart", 3000, "d1").Data!;

    private static UcpCheckoutSession NewSession(IReadOnlyList<UcpLineItem>? items = null)
        => UcpCheckoutSession.Create("ucp_1", "TRY", Links(), 6, Now, items).Data!;

    [Fact]
    public void Create_Defaults_IncompleteAndExpiresIn6Hours()
    {
        var s = NewSession(Items());
        s.Status.ShouldBe(UcpSessionStatus.Incomplete);
        s.Currency.ShouldBe("TRY");
        s.ExpiresAt.ShouldBe(Now.AddHours(6));
        s.Totals.SubtotalMinor.ShouldBe(10000);
    }

    [Fact]
    public void Create_NonTryCurrency_Rejected()
        => UcpCheckoutSession.Create("ucp_1", "USD", Links(), 6, Now).IsSuccess.ShouldBeFalse();

    [Fact]
    public void Create_NoLinks_Rejected()
        => UcpCheckoutSession.Create("ucp_1", "TRY", [], 6, Now).IsSuccess.ShouldBeFalse();

    [Fact]
    public void ReplaceLineItems_Empty_Rejected()
    {
        var s = NewSession(Items());
        s.ReplaceLineItems([]).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ReplaceLineItems_Recomputes_Subtotal()
    {
        var s = NewSession();
        s.ReplaceLineItems(Items(2500, 4)).IsSuccess.ShouldBeTrue();
        s.Totals.SubtotalMinor.ShouldBe(10000);
    }

    [Fact]
    public void SelectFulfillment_ReflectsShippingInTotals()
    {
        var s = NewSession(Items());
        s.SelectFulfillment(Shipping());
        s.Totals.ShippingTotalMinor.ShouldBe(3000);
        s.Totals.GrandTotalMinor.ShouldBe(13000);
    }

    [Fact]
    public void ApplyDiscounts_ReflectsDiscountInTotals()
    {
        var s = NewSession(Items());
        var discount = UcpAppliedDiscount.Create("Kod", 2000, "BOOK10").Data!;
        s.ApplyDiscounts([discount]);
        s.Totals.DiscountTotalMinor.ShouldBe(2000);
        s.Totals.GrandTotalMinor.ShouldBe(8000);
    }

    [Fact]
    public void MarkReadyIfComplete_MissingBuyerOrFulfillment_StaysIncomplete()
    {
        var s = NewSession(Items());
        var r = s.MarkReadyIfComplete(Now);
        r.IsSuccess.ShouldBeFalse();
        s.Status.ShouldBe(UcpSessionStatus.Incomplete);
    }

    [Fact]
    public void MarkReadyIfComplete_AllPresent_Ready()
    {
        var s = NewSession(Items());
        s.SetBuyer(Buyer());
        s.SelectFulfillment(Shipping());
        s.MarkReadyIfComplete(Now).IsSuccess.ShouldBeTrue();
        s.Status.ShouldBe(UcpSessionStatus.ReadyForComplete);
    }

    [Fact]
    public void BeginComplete_NotReady_Rejected()
    {
        var s = NewSession(Items());
        s.BeginComplete("k1", Now).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void BeginComplete_Expired_Rejected()
    {
        var s = ReadySession();
        s.BeginComplete("k1", Now.AddHours(7)).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void BeginComplete_Ready_MovesToInProgress()
    {
        var s = ReadySession();
        s.BeginComplete("k1", Now).IsSuccess.ShouldBeTrue();
        s.Status.ShouldBe(UcpSessionStatus.CompleteInProgress);
    }

    [Fact]
    public void Complete_Idempotent_SameKeyNoError_AfterCompleted()
    {
        var s = ReadySession();
        s.BeginComplete("k1", Now);
        s.MarkCompleted("ORDER-1");
        // Tekrar aynı complete → idempotent Ok, OrderRef değişmez, yeni sipariş yok.
        s.BeginComplete("k1", Now).IsSuccess.ShouldBeTrue();
        s.Status.ShouldBe(UcpSessionStatus.Completed);
        s.OrderRef.ShouldBe("ORDER-1");
    }

    [Fact]
    public void FailComplete_RewindsToReady_ClearsKey()
    {
        var s = ReadySession();
        s.BeginComplete("k1", Now);
        s.FailComplete().IsSuccess.ShouldBeTrue();
        s.Status.ShouldBe(UcpSessionStatus.ReadyForComplete);
        // Yeni deneme yeni key alabilir (idempotency temizlendi).
        s.BeginComplete("k2", Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Cancel_NonTerminal_Canceled()
    {
        var s = NewSession(Items());
        s.Cancel("vazgeçti").IsSuccess.ShouldBeTrue();
        s.Status.ShouldBe(UcpSessionStatus.Canceled);
    }

    [Fact]
    public void Cancel_Completed_Rejected()
    {
        var s = ReadySession();
        s.BeginComplete("k1", Now);
        s.MarkCompleted("ORDER-1");
        s.Cancel(null).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void MutateAfterCancel_Rejected()
    {
        var s = NewSession(Items());
        s.Cancel(null);
        s.ReplaceLineItems(Items()).IsSuccess.ShouldBeFalse();
        s.SetBuyer(Buyer()).IsSuccess.ShouldBeFalse();
    }

    private static UcpCheckoutSession ReadySession()
    {
        var s = NewSession(Items());
        s.SetBuyer(Buyer());
        s.SelectFulfillment(Shipping());
        s.MarkReadyIfComplete(Now);
        return s;
    }
}