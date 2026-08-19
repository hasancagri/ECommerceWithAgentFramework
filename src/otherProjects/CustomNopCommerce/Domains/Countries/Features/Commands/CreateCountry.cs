namespace CustomNopCommerce.Domains.Countries.Features.Commands;

/// <summary>Yeni ülke oluşturma write-slice'ı.</summary>
public static class CreateCountry
{
    public record CreateCountryCommand(
        string Name,
        string? TwoLetterIsoCode,
        string? ThreeLetterIsoCode,
        bool AllowsBilling,
        bool AllowsShipping,
        bool SubjectToVat,
        bool Published,
        int DisplayOrder);

    public class CreateCountryResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateCountryCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateCountryResponse>> Handle(
            CreateCountryCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateCountryResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = DirectoryResourceConstants.COUNTRY_NAME_REQUIRED });

            var country = Country.Create(cmd.Name, cmd.TwoLetterIsoCode, cmd.ThreeLetterIsoCode,
                cmd.AllowsBilling, cmd.AllowsShipping, cmd.SubjectToVat, cmd.Published, cmd.DisplayOrder);
            session.Store(country);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateCountryResponse>.Ok(
                new CreateCountryResponse { Id = country.Id });
        }
    }
}

public static class CreateCountryCommandEndpoint
{
    public static RouteGroupBuilder CreateCountryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateCountry.CreateCountryCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateCountry.CreateCountryResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateCountry");
        return group;
    }
}
