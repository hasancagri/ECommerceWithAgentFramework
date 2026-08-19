using CustomNopCommerce.Domains.ProductAttributeMappings.ValueObjects;
using CustomNopCommerce.Domains.ProductAttributes;
using CustomNopCommerce.Domains.Products;

namespace CustomNopCommerce.Domains.ProductAttributeMappings.Features.Commands;

/// <summary>Bir global özniteliği bir ürüne bağlama (eşleme oluşturma) write-slice'ı.</summary>
public static class CreateProductAttributeMapping
{
    public record CreateProductAttributeMappingCommand(
        Guid ProductId,
        Guid ProductAttributeId,
        AttributeControlType ControlType,
        bool IsRequired,
        int DisplayOrder,
        string? TextPrompt,
        int? MinLength,
        int? MaxLength);

    public class CreateProductAttributeMappingResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateProductAttributeMappingCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateProductAttributeMappingResponse>> Handle(
            CreateProductAttributeMappingCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            // Ürün + öznitelik var olmalı (Catalog BC içi Id referansları).
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<CreateProductAttributeMappingResponse>.Error(new MessageItem
                { Property = nameof(cmd.ProductId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            var attribute = await session.LoadAsync<ProductAttribute>(cmd.ProductAttributeId, ct);
            if (attribute is null || attribute.IsDeleted)
                return FeatureObjectResultModel<CreateProductAttributeMappingResponse>.Error(new MessageItem
                { Property = nameof(cmd.ProductAttributeId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            var validation = AttributeValidationRule.Create(cmd.MinLength, cmd.MaxLength, null, null);
            var mapping = ProductAttributeMapping.Create(cmd.ProductId, cmd.ProductAttributeId,
                cmd.ControlType, cmd.IsRequired, cmd.DisplayOrder, cmd.TextPrompt, validation);

            session.Store(mapping);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateProductAttributeMappingResponse>.Ok(
                new CreateProductAttributeMappingResponse { Id = mapping.Id });
        }
    }
}

public static class CreateProductAttributeMappingCommandEndpoint
{
    public static RouteGroupBuilder CreateProductAttributeMappingGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateProductAttributeMapping.CreateProductAttributeMappingCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateProductAttributeMapping.CreateProductAttributeMappingResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateProductAttributeMapping");
        return group;
    }
}
