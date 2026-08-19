namespace Catalog.Api.Domains.Products.Features.Commands;

public static class SetProductDimensions
{
    public record SetProductDimensionsCommand(Guid Id, decimal Weight, decimal Length, decimal Width, decimal Height);

    public class SetProductDimensionsResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class SetProductDimensionsCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetProductDimensionsResponse>> Handle(
            SetProductDimensionsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<SetProductDimensionsResponse>.NotFound();

            var dimensions = ProductDimensions.Create(cmd.Weight, cmd.Length, cmd.Width, cmd.Height);
            if (dimensions is null)
                return FeatureObjectResultModel<SetProductDimensionsResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Weight),
                    Code = CatalogResourceConstants.PRODUCT_DIMENSIONS_INVALID
                });

            var set = product.SetDimensions(dimensions);
            if (!set.IsSuccess)
                return FeatureObjectResultModel<SetProductDimensionsResponse>.Error(set.Messages);

            session.Store(product);
            return FeatureObjectResultModel<SetProductDimensionsResponse>.Ok(
                new SetProductDimensionsResponse { Id = product.Id });
        }
    }
}

public static class SetProductDimensionsCommandEndpoint
{
    public static RouteGroupBuilder SetProductDimensionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/dimensions", async ([FromBody] SetProductDimensions.SetProductDimensionsCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SetProductDimensions.SetProductDimensionsResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("SetProductDimensions");
        return group;
    }
}