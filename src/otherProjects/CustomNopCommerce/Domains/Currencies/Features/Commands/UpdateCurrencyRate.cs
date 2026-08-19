namespace CustomNopCommerce.Domains.Currencies.Features.Commands;

/// <summary>Bir para biriminin kurunu güncelleyen write-slice'ı. Pozitif olmalı (aggregate invariant'ı).</summary>
public static class UpdateCurrencyRate
{
    public record UpdateCurrencyRateCommand(Guid Id, decimal Rate);

    [Transactional]
    public class UpdateCurrencyRateCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            UpdateCurrencyRateCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var currency = await session.LoadAsync<Currency>(cmd.Id, ct);
            if (currency is null || currency.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = currency.UpdateRate(cmd.Rate);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(currency);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class UpdateCurrencyRateCommandEndpoint
{
    public static RouteGroupBuilder UpdateCurrencyRateGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}/rate", async (Guid id,
            [FromBody] UpdateCurrencyRate.UpdateCurrencyRateCommand body, IMessageBus bus) =>
            {
                var cmd = body with { Id = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("UpdateCurrencyRate");
        return group;
    }
}
