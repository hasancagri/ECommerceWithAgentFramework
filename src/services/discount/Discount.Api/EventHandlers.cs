namespace Discount.Api;

// Sinif adi Wolverine'in handler kesif kurali geregi "Handler" ile bitmeli; "EventHandlers"
// (cogul) kesfedilmez ve mesajlar "no known handler" ile sessizce dusurulur.
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