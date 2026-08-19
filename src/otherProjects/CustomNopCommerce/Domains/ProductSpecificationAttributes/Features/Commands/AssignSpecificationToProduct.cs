using CustomNopCommerce.Domains.Products;
using CustomNopCommerce.Domains.SpecificationAttributes;

namespace CustomNopCommerce.Domains.ProductSpecificationAttributes.Features.Commands;

/// <summary>Bir spesifikasyonu bir ürüne atama write-slice'ı. Türe göre alan zorunluluğu + seçenek
/// aidiyeti burada denetlenir (aggregate invariant'ı handler'da kapatılır).</summary>
public static class AssignSpecificationToProduct
{
    public record AssignSpecificationToProductCommand(
        Guid ProductId,
        Guid SpecificationAttributeId,
        SpecificationAttributeType Type,
        Guid? OptionId,
        string? CustomValue,
        bool AllowFiltering,
        bool ShowOnProductPage,
        int DisplayOrder);

    public class AssignSpecificationToProductResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AssignSpecificationToProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<AssignSpecificationToProductResponse>> Handle(
            AssignSpecificationToProductCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AssignSpecificationToProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.ProductId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            var spec = await session.LoadAsync<SpecificationAttribute>(cmd.SpecificationAttributeId, ct);
            if (spec is null || spec.IsDeleted)
                return FeatureObjectResultModel<AssignSpecificationToProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.SpecificationAttributeId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            // Invariant: Option türü seçenek Id ister; seçenek spec'e ait olmalı. Custom türler değer ister.
            if (cmd.Type == SpecificationAttributeType.Option)
            {
                if (cmd.OptionId is not { } optionId || spec.Options.All(o => o.Id != optionId))
                    return FeatureObjectResultModel<AssignSpecificationToProductResponse>.Error(new MessageItem
                    { Property = nameof(cmd.OptionId), Code = CatalogResourceConstants.SPEC_OPTION_REQUIRED_FOR_OPTION_TYPE });
            }
            else if (string.IsNullOrWhiteSpace(cmd.CustomValue))
            {
                return FeatureObjectResultModel<AssignSpecificationToProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.CustomValue), Code = CatalogResourceConstants.SPEC_CUSTOM_VALUE_REQUIRED });
            }

            var assignment = ProductSpecificationAttribute.Create(cmd.ProductId, cmd.SpecificationAttributeId,
                cmd.Type, cmd.OptionId, cmd.CustomValue, cmd.AllowFiltering, cmd.ShowOnProductPage, cmd.DisplayOrder);

            session.Store(assignment);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<AssignSpecificationToProductResponse>.Ok(
                new AssignSpecificationToProductResponse { Id = assignment.Id });
        }
    }
}

public static class AssignSpecificationToProductCommandEndpoint
{
    public static RouteGroupBuilder AssignSpecificationToProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] AssignSpecificationToProduct.AssignSpecificationToProductCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AssignSpecificationToProduct.AssignSpecificationToProductResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("AssignSpecificationToProduct");
        return group;
    }
}
