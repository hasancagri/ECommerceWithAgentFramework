namespace CustomNopCommerce.Domains.Currencies.Features.Commands;

/// <summary>Yeni para birimi oluşturma write-slice'ı.</summary>
public static class CreateCurrency
{
    public record CreateCurrencyCommand(string Name, string CurrencyCode, decimal Rate, bool Published, int DisplayOrder);

    public class CreateCurrencyResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateCurrencyCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateCurrencyResponse>> Handle(
            CreateCurrencyCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateCurrencyResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = DirectoryResourceConstants.CURRENCY_NAME_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.CurrencyCode))
                return FeatureObjectResultModel<CreateCurrencyResponse>.Error(new MessageItem
                { Property = nameof(cmd.CurrencyCode), Code = DirectoryResourceConstants.CURRENCY_CODE_REQUIRED });
            if (cmd.Rate <= 0)
                return FeatureObjectResultModel<CreateCurrencyResponse>.Error(new MessageItem
                { Property = nameof(cmd.Rate), Code = DirectoryResourceConstants.CURRENCY_RATE_INVALID });

            var currency = Currency.Create(cmd.Name, cmd.CurrencyCode, cmd.Rate, cmd.Published, cmd.DisplayOrder);
            session.Store(currency);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateCurrencyResponse>.Ok(
                new CreateCurrencyResponse { Id = currency.Id });
        }
    }
}

public static class CreateCurrencyCommandEndpoint
{
    public static RouteGroupBuilder CreateCurrencyGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateCurrency.CreateCurrencyCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateCurrency.CreateCurrencyResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateCurrency");
        return group;
    }
}
