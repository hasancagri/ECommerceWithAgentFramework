using Shared;

namespace Storefront.Api.Domains.StorefrontView.Features.EventHandlers;

// 053 US3: Storefront, Order 'OrderCompleted'ı AYRI kuyrukta .UseDurableInbox() ile exactly-once tüketir
// (Program.cs; view'a YAZMAZ → Sequential storefront.events kuyruğuna girmez). Her item'ı StorefrontView'den
// yazar/kategori ile zenginleştirir + item başına kalıcı DedupKey üretir, 'PurchaseEnriched' yayar (cascade
// → outbox). Python bunu tüketir (son-hat idempotency unique(dedup_key)). Bu, push-only read-model'e tek
// türev-event istisnası (plan Constitution Check gerekçeli): öznitelikler zaten burada, Order→Catalog kuplajı yok.
public class EnrichPurchase
{
    public async Task<IntegrationEvents.PurchaseEnriched?> Handle(
        IntegrationEvents.OrderCompleted evt,
        IQuerySession session,
        CancellationToken ct)
    {
        var items = new List<IntegrationEvents.PurchaseEnrichedItem>();
        foreach (var item in evt.Items)
        {
            // Read-model'de öznitelikler denormalize durur; yoksa null (o kalem o boyutta katkı vermez).
            var view = await session.LoadAsync<StorefrontView>(item.ProductId, ct);
            var author = view?.Authors.FirstOrDefault()?.Name;
            var category = view?.Category;

            items.Add(new IntegrationEvents.PurchaseEnrichedItem(
                item.ProductId, item.Quantity, item.UnitPrice, author, category,
                // Kalıcı DedupKey: durable inbox exactly-once → bir kez üretilir, outbox'ta sabit kalır.
                DedupKey: Guid.NewGuid()));
        }

        if (items.Count == 0)
            return null; // kalemsiz sipariş (beklenmez) — yayın yok

        // Order AnonymousId taşımaz (BC izolasyonu) → null; dikiş userId üstünden (FR-013).
        return new IntegrationEvents.PurchaseEnriched(
            evt.OrderId, evt.UserId, AnonymousId: null, evt.OrderedAt, items);
    }
}
