namespace CustomNopCommerce.Domains.Measures.Features.Commands;

/// <summary>Yeni ölçü birimi (boyut veya ağırlık) oluşturma write-slice'ı.</summary>
public static class CreateMeasure
{
    public record CreateMeasureCommand(MeasureType Type, string Name, string SystemKeyword, decimal Ratio, int DisplayOrder);

    public class CreateMeasureResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateMeasureCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateMeasureResponse>> Handle(
            CreateMeasureCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateMeasureResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = DirectoryResourceConstants.MEASURE_NAME_REQUIRED });
            if (cmd.Ratio <= 0)
                return FeatureObjectResultModel<CreateMeasureResponse>.Error(new MessageItem
                { Property = nameof(cmd.Ratio), Code = DirectoryResourceConstants.MEASURE_RATIO_INVALID });

            var measure = Measure.Create(cmd.Type, cmd.Name, cmd.SystemKeyword, cmd.Ratio, cmd.DisplayOrder);
            session.Store(measure);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateMeasureResponse>.Ok(
                new CreateMeasureResponse { Id = measure.Id });
        }
    }
}

public static class CreateMeasureCommandEndpoint
{
    public static RouteGroupBuilder CreateMeasureGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateMeasure.CreateMeasureCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateMeasure.CreateMeasureResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateMeasure");
        return group;
    }
}
