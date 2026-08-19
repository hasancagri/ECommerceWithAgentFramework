namespace Catalog.Api.Domains.Products.Features.Commands;

public static class SetProductSeo
{
    public record SetProductSeoCommand(Guid Id, string? MetaTitle, string? MetaKeywords, string? MetaDescription);

    public class SetProductSeoResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class SetProductSeoCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetProductSeoResponse>> Handle(
            SetProductSeoCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<SetProductSeoResponse>.NotFound();

            var set = product.SetSeo(SeoMetadata.Create(cmd.MetaTitle, cmd.MetaKeywords, cmd.MetaDescription));
            if (!set.IsSuccess)
                return FeatureObjectResultModel<SetProductSeoResponse>.Error(set.Messages);

            session.Store(product);
            return FeatureObjectResultModel<SetProductSeoResponse>.Ok(
                new SetProductSeoResponse { Id = product.Id });
        }
    }
}

public static class SetProductSeoCommandEndpoint
{
    public static RouteGroupBuilder SetProductSeoGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/seo", async ([FromBody] SetProductSeo.SetProductSeoCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SetProductSeo.SetProductSeoResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("SetProductSeo");
        return group;
    }
}