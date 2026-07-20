namespace Discount.Api.Domains.Discounts.Features.Commands;

public static class SetProductDiscount
{
    [RequiredScope(AuthorizationScopes.DiscountWrite)]
    public record SetProductDiscountCommand(Guid ProductId, decimal Rate);

    public class SetProductDiscountResponse
    {
        public Guid ProductId { get; set; }
        public decimal Rate { get; set; }
    }

    [Transactional]
    public class SetProductDiscountCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetProductDiscountResponse>> Handle(
            SetProductDiscountCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            // FR-013: Discount kendi tarafinda yalnizca kendi verisini dogrular; Catalog'a senkron
            // sorgu YAPILMAZ (research.md madde 1) — var olmayan urun kavrami Discount'ta yoktur,
            // sadece kendi mevcut kaydini bulur/olusturur (upsert, FR-012).
            var existing = await session.Query<Discount>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId && !x.IsDeleted, ct);

            Discount discount;
            if (existing is null)
            {
                var createResult = Discount.Create(cmd.ProductId, cmd.Rate);
                if (!createResult.IsSuccess)
                    return FeatureObjectResultModel<SetProductDiscountResponse>.Error(createResult.Messages);

                discount = createResult.Data!;
            }
            else
            {
                var updateResult = existing.UpdateRate(cmd.Rate);
                if (!updateResult.IsSuccess)
                    return FeatureObjectResultModel<SetProductDiscountResponse>.Error(updateResult.Messages);

                discount = updateResult.Data!;
            }

            session.Store(discount);

            // 003-storefront-read-model: writer-publishes — Storefront'un DiscountInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.DiscountChangedEvent(cmd.ProductId, cmd.Rate, DateTime.UtcNow));

            return FeatureObjectResultModel<SetProductDiscountResponse>.Ok(new SetProductDiscountResponse
            {
                ProductId = discount.ProductId,
                Rate = discount.Rate.Value
            });
        }
    }
}

public static class SetProductDiscountCommandEndpoint
{
    public record SetProductDiscountRequest(decimal Rate);

    public static RouteGroupBuilder SetProductDiscountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{productId:guid}",
                async (Guid productId, [FromBody] SetProductDiscountRequest request, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<SetProductDiscount.SetProductDiscountResponse>>(
                        new SetProductDiscount.SetProductDiscountCommand(productId, request.Rate));
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
                })
            .WithName("SetProductDiscount")
            .MapToApiVersion(1, 0)
            .Produces<SetProductDiscount.SetProductDiscountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuthorizationScopes.DiscountWrite);

        return group;
    }
}