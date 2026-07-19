namespace Discount.Api;

public static class OrderCreatedHandler
{
    public static async Task Handle(IntegrationEvents.OrderCreatedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var result = Domains.Discounts.Discount.Create(evt.UserId, DiscountCodeGenerator.Generate(), 0.1m);
        if (!result.IsSuccess) return;

        session.Store(result.Data!);
        await session.SaveChangesAsync(ct);
    }
}