namespace CustomNopCommerce.Domains.SpecificationAttributes.Features.Commands;

/// <summary>Bir spesifikasyona önceden tanımlı seçenek (ör. Ekran Boyutu→"6.1 inç") ekleme write-slice'ı.</summary>
public static class AddSpecificationOption
{
    public record AddSpecificationOptionCommand(
        Guid SpecificationAttributeId,
        string Name,
        string? ColorSquaresRgb,
        int DisplayOrder);

    public class AddSpecificationOptionResponse
    {
        public Guid OptionId { get; set; }
    }

    [Transactional]
    public class AddSpecificationOptionCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddSpecificationOptionResponse>> Handle(
            AddSpecificationOptionCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var spec = await session.LoadAsync<SpecificationAttribute>(cmd.SpecificationAttributeId, ct);
            if (spec is null || spec.IsDeleted)
                return FeatureObjectResultModel<AddSpecificationOptionResponse>.NotFound();

            var result = spec.AddOption(cmd.Name, cmd.ColorSquaresRgb, cmd.DisplayOrder);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddSpecificationOptionResponse>.Error(result.Messages);

            session.Update(spec);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<AddSpecificationOptionResponse>.Ok(
                new AddSpecificationOptionResponse { OptionId = result.Data });
        }
    }
}

public static class AddSpecificationOptionCommandEndpoint
{
    public static RouteGroupBuilder AddSpecificationOptionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/options", async (Guid id,
            [FromBody] AddSpecificationOption.AddSpecificationOptionCommand body, IMessageBus bus) =>
            {
                var cmd = body with { SpecificationAttributeId = id };
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddSpecificationOption.AddSpecificationOptionResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("AddSpecificationOption");
        return group;
    }
}
