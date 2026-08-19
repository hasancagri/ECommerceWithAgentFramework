namespace CustomNopCommerce.Domains.ProductTags.Features.Commands;

/// <summary>Yeni ürün etiketi oluşturma write-slice'ı.</summary>
public static class CreateProductTag
{
    public record CreateProductTagCommand(string Name);

    public class CreateProductTagResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateProductTagCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateProductTagResponse>> Handle(
            CreateProductTagCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateProductTagResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.TAG_NAME_REQUIRED });

            var tag = ProductTag.Create(cmd.Name);
            session.Store(tag);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateProductTagResponse>.Ok(new CreateProductTagResponse { Id = tag.Id });
        }
    }
}

public static class CreateProductTagCommandEndpoint
{
    public static RouteGroupBuilder CreateProductTagGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateProductTag.CreateProductTagCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateProductTag.CreateProductTagResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateProductTag");
        return group;
    }
}
