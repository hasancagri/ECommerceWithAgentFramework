namespace Catalog.Api.Domains.Products.Features.Commands;

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
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<SetProductPublishedResponse>.NotFound();

            var result = cmd.Published ? product.Publish() : product.Unpublish();
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetProductPublishedResponse>.Error(result.Messages);

            // Published dış kontrata binmez (ProductChangedEvent alanı yok); agent okumaları filtreler.
            session.Store(product);
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
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("SetProductPublished");
        return group;
    }
}