namespace Catalog.Api.Domains.Brands.Features.Commands;

public static class CreateBrand
{
    public record CreateBrandCommand(string Name);

    public class CreateBrandResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateBrandCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateBrandResponse>> Handle(
            CreateBrandCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // REST yaratımı bilinçlidir: aynı ad varsa açık hata (feed yolu get-or-create Upsert'te).
            var normalized = NameNormalization.Normalize(cmd.Name ?? string.Empty);
            var exists = await session.Query<Brand>()
                .AnyAsync(x => x.NormalizedName == normalized && !x.IsDeleted, ct);
            if (exists)
                return FeatureObjectResultModel<CreateBrandResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.BRAND_ALREADY_EXISTS
                });

            var created = Brand.Create(cmd.Name!);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreateBrandResponse>.Error(created.Messages);

            session.Store(created.Data!);
            return FeatureObjectResultModel<CreateBrandResponse>.Ok(
                new CreateBrandResponse { Id = created.Data!.Id });
        }
    }
}

public static class CreateBrandCommandEndpoint
{
    public static RouteGroupBuilder CreateBrandGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateBrand.CreateBrandCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateBrand.CreateBrandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateBrand");
        return group;
    }
}