namespace Catalog.Api.Domains.Products.Features.Commands;

public static class RemoveTagFromProduct
{
    public record RemoveTagFromProductCommand(Guid ProductId, Guid TagId);

    public class RemoveTagFromProductResponse
    {
        public Guid ProductId { get; set; }
        public Guid TagId { get; set; }
    }

    [Transactional]
    public class RemoveTagFromProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<RemoveTagFromProductResponse>> Handle(
            RemoveTagFromProductCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<RemoveTagFromProductResponse>.NotFound();

            var remove = product.RemoveTag(cmd.TagId);
            if (!remove.IsSuccess)
                return FeatureObjectResultModel<RemoveTagFromProductResponse>.Error(remove.Messages);

            session.Store(product);
            return FeatureObjectResultModel<RemoveTagFromProductResponse>.Ok(
                new RemoveTagFromProductResponse { ProductId = product.Id, TagId = cmd.TagId });
        }
    }
}

public static class RemoveTagFromProductCommandEndpoint
{
    public static RouteGroupBuilder RemoveTagFromProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{productId:guid}/tags/{tagId:guid}", async (Guid productId, Guid tagId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RemoveTagFromProduct.RemoveTagFromProductResponse>>(
                    new RemoveTagFromProduct.RemoveTagFromProductCommand(productId, tagId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("RemoveTagFromProduct");
        return group;
    }
}