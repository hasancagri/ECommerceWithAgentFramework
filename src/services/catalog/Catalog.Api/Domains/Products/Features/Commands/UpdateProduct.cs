using Common;
using Common.Utils.Authorization;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Mvc;
using Shared.Enums;
using Wolverine.Attributes;

namespace Catalog.Api.Domains.Products.Features.Commands;

public static class UpdateProduct
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record UpdateProductCommand(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        string Sku,
        BrandType Brand,
        string? ImageUrl);

    public class UpdateProductResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class UpdateProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateProductResponse>> Handle(
            UpdateProductCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<UpdateProductResponse>.NotFound();

            product.Update(cmd.Name, cmd.Description, cmd.Price, cmd.Sku, cmd.Brand, cmd.ImageUrl);
            session.Store(product);

            return FeatureObjectResultModel<UpdateProductResponse>.Ok(new UpdateProductResponse { Id = product.Id });
        }
    }
}

public static class UpdateProductCommandEndpoint
{
    public static RouteGroupBuilder UpdateProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/", async ([FromBody] UpdateProduct.UpdateProductCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("UpdateProduct")
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);
        return group;
    }
}