namespace CustomNopCommerce.Domains.Countries.Features.Commands;

/// <summary>Bir ülkeye il/eyalet ekleme write-slice'ı.</summary>
public static class AddStateProvince
{
    public record AddStateProvinceCommand(Guid CountryId, string Name, string? Abbreviation, int DisplayOrder, bool Published);

    public class AddStateProvinceResponse
    {
        public Guid StateId { get; set; }
    }

    [Transactional]
    public class AddStateProvinceCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddStateProvinceResponse>> Handle(
            AddStateProvinceCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var country = await session.LoadAsync<Country>(cmd.CountryId, ct);
            if (country is null || country.IsDeleted)
                return FeatureObjectResultModel<AddStateProvinceResponse>.NotFound();

            var result = country.AddStateProvince(cmd.Name, cmd.Abbreviation, cmd.DisplayOrder, cmd.Published);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddStateProvinceResponse>.Error(result.Messages);

            session.Update(country);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<AddStateProvinceResponse>.Ok(
                new AddStateProvinceResponse { StateId = result.Data });
        }
    }
}

public static class AddStateProvinceCommandEndpoint
{
    public static RouteGroupBuilder AddStateProvinceGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/states", async (Guid id,
            [FromBody] AddStateProvince.AddStateProvinceCommand body, IMessageBus bus) =>
            {
                var cmd = body with { CountryId = id };
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddStateProvince.AddStateProvinceResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("AddStateProvince");
        return group;
    }
}
