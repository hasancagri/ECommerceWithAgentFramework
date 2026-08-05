namespace Stock.Api.Tests;

public class ProductStockTests
{
    [Fact]
    public void Create_SetsProductIdAndQuantity()
    {
        var productId = Guid.NewGuid();

        var stock = ProductStock.Create(productId, 10);

        stock.ProductId.ShouldBe(productId);
        stock.Quantity.ShouldBe(10);
    }

    [Fact]
    public void Increase_AddsToQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        stock.Increase(5);

        stock.Quantity.ShouldBe(15);
    }

    [Fact]
    public void Decrease_SubtractsFromQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        stock.Decrease(4);

        stock.Quantity.ShouldBe(6);
    }

    [Fact]
    public void Decrease_BelowZero_GoesNegative_NoGuard()
    {
        // Mevcut davranisi belgeler: Decrease negatif stoga izin verir (guard yok).
        // Ileride guard eklenirse bu test kirmizi bayrak olur.
        var stock = ProductStock.Create(Guid.NewGuid(), 3);

        stock.Decrease(5);

        stock.Quantity.ShouldBe(-2);
    }

    [Fact]
    public void SetQuantity_SetsAbsoluteValue()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.SetQuantity(42);

        result.IsSuccess.ShouldBeTrue();
        stock.Quantity.ShouldBe(42);
    }

    [Fact]
    public void SetQuantity_Zero_IsAllowed()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.SetQuantity(0);

        result.IsSuccess.ShouldBeTrue();
        stock.Quantity.ShouldBe(0);
    }

    [Fact]
    public void SetQuantity_Negative_ReturnsError_AndDoesNotChangeQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        var result = stock.SetQuantity(-1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE);
        stock.Quantity.ShouldBe(10);
    }

    // --- 012-stock-reservation ---

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    [Fact]
    public void SetReservedQuantity_ReservesAndReducesAvailable()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();

        var result = stock.SetReservedQuantity(user, 3, Ttl, now);

        result.IsSuccess.ShouldBeTrue();
        stock.AvailableAt(now).ShouldBe(2);
        stock.OnHand.ShouldBe(5); // OnHand degismez
    }

    [Fact]
    public void SetReservedQuantity_ExceedingAvailable_ReturnsInsufficient()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);

        var result = stock.SetReservedQuantity(Guid.NewGuid(), 6, Ttl, now);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_INSUFFICIENT);
        stock.AvailableAt(now).ShouldBe(5);
    }

    [Fact]
    public void LastUnit_SecondUserCannotReserve()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 1);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        stock.SetReservedQuantity(a, 1, Ttl, now).IsSuccess.ShouldBeTrue();
        stock.AvailableAt(now).ShouldBe(0);

        var bResult = stock.SetReservedQuantity(b, 1, Ttl, now);
        bResult.IsSuccess.ShouldBeFalse();
        bResult.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_INSUFFICIENT);
    }

    [Fact]
    public void SetReservedQuantity_IsIdempotent_AndDoesNotRenewExpiresAt()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();

        stock.SetReservedQuantity(user, 2, Ttl, now);
        var firstExpiry = stock.Reservations.Single().ExpiresAt;

        // Sonraki set (farkli now): sabit TTL — ExpiresAt YENILENMEZ.
        stock.SetReservedQuantity(user, 4, Ttl, now.AddMinutes(5));

        var res = stock.Reservations.Single();
        res.Quantity.ShouldBe(4);
        res.ExpiresAt.ShouldBe(firstExpiry);
        stock.AvailableAt(now).ShouldBe(1);
    }

    [Fact]
    public void SetReservedQuantity_Zero_RemovesReservation()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();

        stock.SetReservedQuantity(user, 3, Ttl, now);
        stock.SetReservedQuantity(user, 0, Ttl, now);

        stock.Reservations.ShouldBeEmpty();
        stock.AvailableAt(now).ShouldBe(5);
    }

    [Fact]
    public void Release_FreesReservation()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();
        stock.SetReservedQuantity(user, 3, Ttl, now);

        stock.Release(user).IsSuccess.ShouldBeTrue();

        stock.AvailableAt(now).ShouldBe(5);
        stock.OnHand.ShouldBe(5);
    }

    [Fact]
    public void Commit_ReducesOnHand_AndClosesReservation()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();
        stock.SetReservedQuantity(user, 2, Ttl, now);

        var result = stock.Commit(user, 2, now);

        result.IsSuccess.ShouldBeTrue();
        stock.OnHand.ShouldBe(3);
        stock.Reservations.ShouldBeEmpty();
        stock.AvailableAt(now).ShouldBe(3);
    }

    [Fact]
    public void Commit_WithoutActiveReservation_ReturnsError()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);

        var result = stock.Commit(Guid.NewGuid(), 1, now);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == StockResourceConstants.STOCK_NO_ACTIVE_RESERVATION);
        stock.OnHand.ShouldBe(5);
    }

    [Fact]
    public void ExpiredReservation_NotCountedInAvailable_LazyFilter()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 1);
        var user = Guid.NewGuid();
        stock.SetReservedQuantity(user, 1, Ttl, now);

        var later = now.Add(Ttl).AddSeconds(1);
        stock.AvailableAt(later).ShouldBe(1); // sure gecti — lazy filtre sayamaz
    }

    [Fact]
    public void PurgeExpired_RemovesOnlyExpired_AndReturnsThem()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 10);
        var expiredUser = Guid.NewGuid();
        var activeUser = Guid.NewGuid();
        stock.SetReservedQuantity(expiredUser, 2, TimeSpan.FromMinutes(1), now);
        stock.SetReservedQuantity(activeUser, 3, TimeSpan.FromMinutes(30), now);

        var later = now.AddMinutes(5);
        var purged = stock.PurgeExpired(later);

        purged.Count.ShouldBe(1);
        purged.Single().UserId.ShouldBe(expiredUser);
        stock.Reservations.Count.ShouldBe(1);
        stock.Reservations.Single().UserId.ShouldBe(activeUser);
    }

    // --- 017-basket-reservation-anchor: mutlak expiresAt ---

    [Fact]
    public void SetReservedQuantity_WithExplicitExpiresAt_NewReservationUsesAnchor()
    {
        var now = DateTimeOffset.UnixEpoch;
        var anchor = now.AddMinutes(5);
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();

        stock.SetReservedQuantity(user, 2, Ttl, now, anchor).IsSuccess.ShouldBeTrue();

        stock.Reservations.Single().ExpiresAt.ShouldBe(anchor);
    }

    [Fact]
    public void SetReservedQuantity_WithExplicitExpiresAt_ExistingReservationAlignedToAnchor()
    {
        var now = DateTimeOffset.UnixEpoch;
        var anchor = now.AddMinutes(5);
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();
        stock.SetReservedQuantity(user, 2, Ttl, now); // eski yol: now + Ttl

        stock.SetReservedQuantity(user, 3, Ttl, now.AddMinutes(1), anchor);

        var res = stock.Reservations.Single();
        res.Quantity.ShouldBe(3);
        res.ExpiresAt.ShouldBe(anchor); // capaya esitlendi
    }

    [Fact]
    public void SetReservedQuantity_WithExplicitExpiresAt_ExpiredUnsweptReservation_RebornWithAnchor()
    {
        // R2 kritik bug: dolmus ama supurulmemis rezervasyon; eski davranis aninda-dolmus rezervasyon uretirdi.
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();
        stock.SetReservedQuantity(user, 2, TimeSpan.FromMinutes(1), now);

        var later = now.AddMinutes(10); // rezervasyon dolmus, sweep henuz kosmamis
        var anchor = later.AddMinutes(5);
        stock.SetReservedQuantity(user, 1, Ttl, later, anchor).IsSuccess.ShouldBeTrue();

        var res = stock.Reservations.Single();
        res.ExpiresAt.ShouldBe(anchor);
        res.IsActiveAt(later).ShouldBeTrue();
    }

    [Fact]
    public void SetReservedQuantity_NullExpiresAt_KeepsLegacyFixedTtlBehavior()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 5);
        var user = Guid.NewGuid();

        stock.SetReservedQuantity(user, 2, Ttl, now);
        var firstExpiry = stock.Reservations.Single().ExpiresAt;
        firstExpiry.ShouldBe(now.Add(Ttl));

        stock.SetReservedQuantity(user, 4, Ttl, now.AddMinutes(5)); // null => yenileme yok
        stock.Reservations.Single().ExpiresAt.ShouldBe(firstExpiry);
    }

    [Fact]
    public void Oversell_SupplierBelowReserved_AvailableClampedToZero_AndFlagged()
    {
        var now = DateTimeOffset.UnixEpoch;
        var stock = ProductStock.Create(Guid.NewGuid(), 3);
        var user = Guid.NewGuid();
        stock.SetReservedQuantity(user, 3, Ttl, now);

        // Tedarikci OnHand'i 1'e dusurur (aktif rezervasyon 3 > 1 => oversell).
        stock.SetQuantity(1);

        stock.AvailableAt(now).ShouldBe(0);     // negatif olmaz (G1/FR-017)
        stock.IsOversoldAt(now).ShouldBeTrue(); // handler bunu log'lar
    }

    // 026 (US2/FR-005): bayat durable tetik fire olsa bile aktif (yenilenmis) rezervasyon
    // PurgeExpired ile SILINMEZ; yalniz suresi gecmis olan silinir.
    [Fact]
    public void PurgeExpired_LeavesActiveReservation_RemovesOnlyExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var stock = ProductStock.Create(Guid.NewGuid(), 10);
        var user = Guid.NewGuid();

        // Aktif rezervasyon: bitis now+5dk (ileri).
        stock.SetReservedQuantity(user, 2, TimeSpan.FromMinutes(5), now, now.AddMinutes(5));

        // Bayat tetik anini simule et (now) => rezervasyon HALA aktif => hicbir sey silinmez.
        var purgedEarly = stock.PurgeExpired(now);

        purgedEarly.Count.ShouldBe(0);
        stock.Reservations.Count.ShouldBe(1);

        // Gercek bitisten sonra purge => rezervasyon silinir.
        var purgedLate = stock.PurgeExpired(now.AddMinutes(6));

        purgedLate.Count.ShouldBe(1);
        stock.Reservations.Count.ShouldBe(0);
    }
}
