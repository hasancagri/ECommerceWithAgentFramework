using Common;
using Common.Utils.Constants;
using Wolverine.Attributes;

namespace Catalog.Api.Domains.Products.Features.Commands;

public static class DeleteProduct
{
    public record DeleteProductCommand(Guid Id);

    public class DeleteProductResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class DeleteProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<DeleteProductResponse>> Handle(
            DeleteProductCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<DeleteProductResponse>.NotFound();

            product.Delete();
            session.Store(product);

            return FeatureObjectResultModel<DeleteProductResponse>.Ok(new DeleteProductResponse { Id = product.Id });
        }
    }
}

public static class DeleteProductCommandEndpoint
{
    public static RouteGroupBuilder DeleteProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>>(
                    new DeleteProduct.DeleteProductCommand(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("DeleteProduct")
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);
        return group;
    }
}