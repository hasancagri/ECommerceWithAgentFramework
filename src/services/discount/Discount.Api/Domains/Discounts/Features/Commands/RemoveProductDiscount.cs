namespace Discount.Api.Domains.Discounts.Features.Commands;

public static class RemoveProductDiscount
{
    [RequiredScope(AuthorizationScopes.DiscountWrite)]
    public record RemoveProductDiscountCommand(Guid ProductId);

    [Transactional]
    public class RemoveProductDiscountCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            RemoveProductDiscountCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var discount = await session.Query<Discount>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId && !x.IsDeleted, ct);

            if (discount is null)
                return FeatureResultModel.NotFound();

            discount.Delete();
            session.Store(discount);

            // 003-storefront-read-model: writer-publishes — Rate: null, Storefront'ta indirimi kaldirir.
            await bus.PublishAsync(new IntegrationEvents.DiscountChangedEvent(cmd.ProductId, Rate: null));

            return FeatureResultModel.Ok();
        }
    }
}

public static class RemoveProductDiscountCommandEndpoint
{
    public static RouteGroupBuilder RemoveProductDiscountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{productId:guid}", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new RemoveProductDiscount.RemoveProductDiscountCommand(productId));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("RemoveProductDiscount")
            .MapToApiVersion(1, 0)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationScopes.DiscountWrite);

        return group;
    }
}