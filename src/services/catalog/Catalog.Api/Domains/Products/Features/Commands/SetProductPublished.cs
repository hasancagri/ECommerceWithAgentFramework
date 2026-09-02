namespace Catalog.Api.Domains.Products.Features.Commands;

// 058: yayın anahtarı vitrine de yansır — Unpublish, ProductChangedEvent(IsDeleted:true) ile Storefront
// satırını gizler (silmez; 016 "silme yok" sürer, IsDeleted read-model'de gizleme bayrağıdır);
// yeniden Publish tam event'le satırı geri açar. Fiyatsız Publish aggregate kapısına takılır (051).
public static class SetProductPublished
{
    public record SetProductPublishedCommand(Guid Id, bool Published);

    public class SetProductPublishedResponse
    {
        public Guid Id { get; set; }
        public bool Published { get; set; }
    }

    [Transactional]
    public class SetProductPublishedCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetProductPublishedResponse>> Handle(
            SetProductPublishedCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<SetProductPublishedResponse>.NotFound();

            var result = cmd.Published ? product.Publish() : product.Unpublish();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetProductPublishedResponse>.Error(result.Messages);

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

            return FeatureObjectResultModel<SetProductPublishedResponse>.Ok(
                new SetProductPublishedResponse { Id = product.Id, Published = product.Published });
        }
    }
}

public static class SetProductPublishedCommandEndpoint
{
    public static RouteGroupBuilder SetProductPublishedGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/published", async ([FromBody] SetProductPublished.SetProductPublishedCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SetProductPublished.SetProductPublishedResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("SetProductPublished");
        return group;
    }
}