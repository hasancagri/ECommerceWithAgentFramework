using CustomNopCommerce.Domains.TaxCategories;

namespace CustomNopCommerce.Domains.TaxRates.Features.Commands;

/// <summary>Bir vergi kategorisine (ve isteğe bağlı ülkeye) oran ekleme write-slice'ı.</summary>
public static class CreateTaxRate
{
    public record CreateTaxRateCommand(Guid TaxCategoryId, Guid? CountryId, decimal Percentage);

    public class CreateTaxRateResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateTaxRateCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateTaxRateResponse>> Handle(
            CreateTaxRateCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            // Vergi kategorisi var olmalı (Tax BC içi Id referansı).
            var category = await session.LoadAsync<TaxCategory>(cmd.TaxCategoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<CreateTaxRateResponse>.Error(new MessageItem
                { Property = nameof(cmd.TaxCategoryId), Code = TaxResourceConstants.RECORD_NOT_FOUND });

            if (cmd.Percentage < 0 || cmd.Percentage > 100)
                return FeatureObjectResultModel<CreateTaxRateResponse>.Error(new MessageItem
                { Property = nameof(cmd.Percentage), Code = TaxResourceConstants.RATE_PERCENTAGE_INVALID });

            var rate = TaxRate.Create(cmd.TaxCategoryId, cmd.CountryId, cmd.Percentage);
            session.Store(rate);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateTaxRateResponse>.Ok(
                new CreateTaxRateResponse { Id = rate.Id });
        }
    }
}

public static class CreateTaxRateCommandEndpoint
{
    public static RouteGroupBuilder CreateTaxRateGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateTaxRate.CreateTaxRateCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateTaxRate.CreateTaxRateResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateTaxRate");
        return group;
    }
}
