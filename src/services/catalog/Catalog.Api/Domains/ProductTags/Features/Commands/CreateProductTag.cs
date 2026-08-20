namespace Catalog.Api.Domains.ProductTags.Features.Commands;

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
        public Task<FeatureObjectResultModel<CreateProductTagResponse>> Handle(
            CreateProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Ad zorunluluğu handler'da (factory düz aggregate döner — ProductTag.Create notu).
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return Task.FromResult(FeatureObjectResultModel<CreateProductTagResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.TAG_NAME_REQUIRED
                }));

            var tag = ProductTag.Create(cmd.Name);
            session.Store(tag);

            // Etiket dış kontrat taşımaz (Storefront/event yok) — yalnız Catalog içi kayıt.
            return Task.FromResult(FeatureObjectResultModel<CreateProductTagResponse>.Ok(
                new CreateProductTagResponse { Id = tag.Id }));
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