
namespace Basket.Api;

public static class EventHandlers
{
    public static async Task Handle(IntegrationEvents.OrderCreatedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var basket = await session.Query<Domains.Baskets.Basket>()
            .FirstOrDefaultAsync(x => x.UserId == evt.UserId, ct);

        if (basket is null) return;

        session.Delete(basket);
        await session.SaveChangesAsync(ct);
    }
}