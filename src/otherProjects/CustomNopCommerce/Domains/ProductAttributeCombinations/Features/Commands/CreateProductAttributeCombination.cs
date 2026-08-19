using CustomNopCommerce.Domains.Products;

namespace CustomNopCommerce.Domains.ProductAttributeCombinations.Features.Commands;

/// <summary>Bir ürün için satılabilir varyant (değer kombinasyonu + SKU) oluşturma write-slice'ı.</summary>
public static class CreateProductAttributeCombination
{
    public record CreateProductAttributeCombinationCommand(
        Guid ProductId,
        string Sku,
        string? Gtin,
        string? ManufacturerPartNumber,
        List<Guid> SelectedValueIds);

    public class CreateProductAttributeCombinationResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateProductAttributeCombinationCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateProductAttributeCombinationResponse>> Handle(
            CreateProductAttributeCombinationCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<CreateProductAttributeCombinationResponse>.Error(new MessageItem
                { Property = nameof(cmd.ProductId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            if (string.IsNullOrWhiteSpace(cmd.Sku))
                return FeatureObjectResultModel<CreateProductAttributeCombinationResponse>.Error(new MessageItem
                { Property = nameof(cmd.Sku), Code = CatalogResourceConstants.COMBINATION_SKU_REQUIRED });

            if (cmd.SelectedValueIds is null || cmd.SelectedValueIds.Count == 0)
                return FeatureObjectResultModel<CreateProductAttributeCombinationResponse>.Error(new MessageItem
                { Property = nameof(cmd.SelectedValueIds), Code = CatalogResourceConstants.COMBINATION_NO_VALUES });

            var combination = ProductAttributeCombination.Create(cmd.ProductId, cmd.Sku, cmd.Gtin,
                cmd.ManufacturerPartNumber, cmd.SelectedValueIds);

            session.Store(combination);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateProductAttributeCombinationResponse>.Ok(
                new CreateProductAttributeCombinationResponse { Id = combination.Id });
        }
    }
}

public static class CreateProductAttributeCombinationCommandEndpoint
{
    public static RouteGroupBuilder CreateProductAttributeCombinationGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateProductAttributeCombination.CreateProductAttributeCombinationCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateProductAttributeCombination.CreateProductAttributeCombinationResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateProductAttributeCombination");
        return group;
    }
}
