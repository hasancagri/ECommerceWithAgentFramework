namespace Basket.Api;

// 028: OrderCreatedEvent handler'i kaldirildi — sepet temizligi CheckoutSaga'nin gRPC adimidir.
public static class BasketEventHandlers
{
    // 012 (US4): TTL dolan rezervasyonun sepet satirini sil (olu satir kalmaz).
    public static async Task Handle(IntegrationEvents.ReservationExpired evt, IDocumentSession session, CancellationToken ct)
    {
        var basket = await session.Query<Domains.Baskets.Basket>()
            .FirstOrDefaultAsync(x => x.UserId == evt.UserId, ct);

        if (basket is null) return;

        var result = basket.RemoveItem(evt.ProductId);
        if (!result.IsSuccess) return; // urun zaten sepette degil

        session.Store(basket);
        await session.SaveChangesAsync(ct);
    }
}