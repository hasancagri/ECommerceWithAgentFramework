namespace CustomNopCommerce.Domains.ProductAttributes.Features.Commands;

/// <summary>Yeni global öznitelik (Renk/Beden...) oluşturma write-slice'ı.</summary>
public static class CreateProductAttribute
{
    public record CreateProductAttributeCommand(string Name, string Description);

    public class CreateProductAttributeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateProductAttributeCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateProductAttributeResponse>> Handle(
            CreateProductAttributeCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateProductAttributeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.ATTRIBUTE_NAME_REQUIRED });

            var attribute = ProductAttribute.Create(cmd.Name, cmd.Description);
            session.Store(attribute);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateProductAttributeResponse>.Ok(
                new CreateProductAttributeResponse { Id = attribute.Id });
        }
    }
}

public static class CreateProductAttributeCommandEndpoint
{
    public static RouteGroupBuilder CreateProductAttributeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateProductAttribute.CreateProductAttributeCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateProductAttribute.CreateProductAttributeResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateProductAttribute");
        return group;
    }
}
