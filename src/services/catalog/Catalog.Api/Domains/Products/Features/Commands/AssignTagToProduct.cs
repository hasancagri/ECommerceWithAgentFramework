namespace Catalog.Api.Domains.Products.Features.Commands;

public static class AssignTagToProduct
{
    public record AssignTagToProductCommand(Guid ProductId, Guid TagId);

    public class AssignTagToProductResponse
    {
        public Guid ProductId { get; set; }
        public Guid TagId { get; set; }
    }

    [Transactional]
    public class AssignTagToProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<AssignTagToProductResponse>> Handle(
            AssignTagToProductCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AssignTagToProductResponse>.NotFound();

            var tag = await session.LoadAsync<ProductTags.ProductTag>(cmd.TagId, ct);
            if (tag is null || tag.IsDeleted)
                return FeatureObjectResultModel<AssignTagToProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.TagId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            var add = product.AddTag(cmd.TagId);
            if (!add.IsSuccess)
                return FeatureObjectResultModel<AssignTagToProductResponse>.Error(add.Messages);

            session.Store(product);
            return FeatureObjectResultModel<AssignTagToProductResponse>.Ok(
                new AssignTagToProductResponse { ProductId = product.Id, TagId = cmd.TagId });
        }
    }
}

public static class AssignTagToProductCommandEndpoint
{
    public static RouteGroupBuilder AssignTagToProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{productId:guid}/tags/{tagId:guid}", async (Guid productId, Guid tagId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AssignTagToProduct.AssignTagToProductResponse>>(
                    new AssignTagToProduct.AssignTagToProductCommand(productId, tagId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("AssignTagToProduct");
        return group;
    }
}