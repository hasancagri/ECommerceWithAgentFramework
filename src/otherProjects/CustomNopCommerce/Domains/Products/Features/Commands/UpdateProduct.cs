using CustomNopCommerce.Domains.Products.ValueObjects;

namespace CustomNopCommerce.Domains.Products.Features.Commands;

/// <summary>Mevcut ürünün temel alanlarını güncelleyen write-slice'ı. Değişmezler aggregate'te korunur.</summary>
public static class UpdateProduct
{
    public record UpdateProductCommand(
        Guid Id,
        string Name,
        decimal Price,
        string ShortDescription,
        string FullDescription);

    [Transactional]
    public class UpdateProductCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            UpdateProductCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureResultModel.NotFound();

            var price = Money.Create(cmd.Price);
            if (price is null)
                return FeatureResultModel.Error(new MessageItem
                { Property = nameof(cmd.Price), Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE });

            var rename = product.Rename(cmd.Name);
            if (!rename.IsSuccess)
                return FeatureResultModel.Error(rename.Messages);

            product.UpdateDescriptions(cmd.ShortDescription, cmd.FullDescription);
            product.SetPrice(price);

            session.Update(product);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class UpdateProductCommandEndpoint
{
    public static RouteGroupBuilder UpdateProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateProduct.UpdateProductCommand body, IMessageBus bus) =>
            {
                var cmd = body with { Id = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("UpdateProduct");
        return group;
    }
}
