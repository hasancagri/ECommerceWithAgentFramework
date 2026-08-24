using Personalization.Api.Constants;
using Personalization.Api.Domains.PurchaseSignals;
using Personalization.Api.Domains.PurchaseSignals.ValueObjects;
using Shouldly;
using Xunit;

namespace Personalization.Api.Tests;

// 048 US1 — PurchaseSignal.Create invariant'lari: en az 1 kalem, gecerli referans, Id=OrderId.
public class PurchaseSignalTests
{
    private static readonly Guid OrderId = Guid.Parse("11110000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("22220000-0000-0000-0000-000000000002");
    private static readonly Guid ProductId = Guid.Parse("33330000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset OrderedAt = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<PurchaseSignalItem> OneItem() =>
        [PurchaseSignalItem.Create(ProductId, "Electronics", "Acme", 2, 50m).Data!];

    [Fact]
    public void Create_Valid_SucceedsWithIdEqualsOrderId()
    {
        var result = PurchaseSignal.Create(OrderId, UserId, OrderedAt, OneItem());

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Id.ShouldBe(OrderId); // idempotent dogal anahtar
        result.Data.UserId.ShouldBe(UserId);
        result.Data.OrderedAt.ShouldBe(OrderedAt);
        result.Data.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_NoItems_Fails()
    {
        var result = PurchaseSignal.Create(OrderId, UserId, OrderedAt, []);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.PURCHASE_SIGNAL_ITEMS_REQUIRED);
    }

    [Fact]
    public void Create_EmptyOrderId_Fails()
    {
        var result = PurchaseSignal.Create(Guid.Empty, UserId, OrderedAt, OneItem());

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.PURCHASE_SIGNAL_REFERENCE_INVALID);
    }

    [Fact]
    public void Create_EmptyUserId_Fails()
    {
        var result = PurchaseSignal.Create(OrderId, Guid.Empty, OrderedAt, OneItem());

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.PURCHASE_SIGNAL_REFERENCE_INVALID);
    }
}