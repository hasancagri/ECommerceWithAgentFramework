namespace Catalog.Api.Domains.Products.Features.Agents;

// 070 US1: yayın anahtarı (agent yüzeyi) — SetProductPublished İKİZİ (bilinçli tekrar). Unpublish
// ProductChangedEvent(IsDeleted:true) ile vitrinden düşürür (016 "silme yok" sürer); Publish tam
// event'le geri açar. Fiyatsız Publish aggregate kapısına takılır (051). İz: AdminActionLog.
public static class AdminSetPublishedForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminSetPublishedCommand(Guid UserId, Guid ProductId, bool Published);

    public class AdminSetPublishedResponse
    {
        public Guid ProductId { get; set; }
        public bool IsPublished { get; set; }
    }

    [Transactional]
    public class AdminSetPublishedForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSetPublishedResponse>> Handle(
            AdminSetPublishedCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.SetPublished, cmd.ProductId.ToString(), "product not found"));
                return FeatureObjectResultModel<AdminSetPublishedResponse>.NotFound();
            }

            var result = cmd.Published ? product.Publish() : product.Unpublish();
            if (!result.IsSuccess)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.SetPublished, product.Id.ToString(),
                    $"publish={cmd.Published} rejected"));
                return FeatureObjectResultModel<AdminSetPublishedResponse>.Error(result.Messages);
            }

            session.Store(product);

            // Fat event künye adlarıyla gider (tüketici Catalog'a lookup yapmaz — 016 R7).
            var authors = (await session.LoadManyAsync<Author>(ct, product.AuthorIds.ToArray()))
                .Select(a => new IntegrationEvents.AuthorRef(a.Id, a.Name)).ToList();
            var publisher = await session.LoadAsync<Publisher>(product.PublisherId, ct);
            var primaryCategoryId = product.Categories.Select(c => c.CategoryId).FirstOrDefault();
            var category = primaryCategoryId == Guid.Empty
                ? null
                : await session.LoadAsync<Category>(primaryCategoryId, ct);

            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.FullDescription, product.Price.Amount,
                authors, product.PublisherId, publisher?.Name ?? string.Empty,
                primaryCategoryId, category?.Name ?? string.Empty,
                product.ImageUrl, IsDeleted: !product.Published));

            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.SetPublished, product.Id.ToString(),
                product.Published ? "published" : "unpublished"));

            return FeatureObjectResultModel<AdminSetPublishedResponse>.Ok(
                new AdminSetPublishedResponse { ProductId = product.Id, IsPublished = product.Published });
        }
    }
}