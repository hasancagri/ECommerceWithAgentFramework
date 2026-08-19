namespace CustomNopCommerce.Domains.TierPrices.Features.Commands;

/// <summary>Bir ürüne kademeli fiyat ekleme write-slice'ı.</summary>
public static class CreateTierPrice
{
    public record CreateTierPriceCommand(
        Guid ProductId,
        Guid? CustomerRoleId,
        int Quantity,
        decimal Price,
        DateTime? StartDateUtc,
        DateTime? EndDateUtc);

    public class CreateTierPriceResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateTierPriceCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateTierPriceResponse>> Handle(
            CreateTierPriceCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (cmd.Quantity <= 0)
                return FeatureObjectResultModel<CreateTierPriceResponse>.Error(new MessageItem
                { Property = nameof(cmd.Quantity), Code = PricingResourceConstants.TIERPRICE_QUANTITY_INVALID });
            if (cmd.Price < 0)
                return FeatureObjectResultModel<CreateTierPriceResponse>.Error(new MessageItem
                { Property = nameof(cmd.Price), Code = PricingResourceConstants.TIERPRICE_PRICE_INVALID });

            var tierPrice = TierPrice.Create(cmd.ProductId, cmd.CustomerRoleId, cmd.Quantity, cmd.Price,
                cmd.StartDateUtc, cmd.EndDateUtc);
            session.Store(tierPrice);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateTierPriceResponse>.Ok(
                new CreateTierPriceResponse { Id = tierPrice.Id });
        }
    }
}

public static class CreateTierPriceCommandEndpoint
{
    public static RouteGroupBuilder CreateTierPriceGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateTierPrice.CreateTierPriceCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateTierPrice.CreateTierPriceResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateTierPrice");
        return group;
    }
}
