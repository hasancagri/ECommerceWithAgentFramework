namespace Catalog.Api.Domains.ProductTags.Features.Commands;

public static class RenameProductTag
{
    public record RenameProductTagCommand(Guid Id, string Name);

    public class RenameProductTagResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class RenameProductTagCommandHandler
    {
        public async Task<FeatureObjectResultModel<RenameProductTagResponse>> Handle(
            RenameProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var tag = await session.LoadAsync<ProductTag>(cmd.Id, ct);
            if (tag is null || tag.IsDeleted)
                return FeatureObjectResultModel<RenameProductTagResponse>.NotFound();

            var rename = tag.Rename(cmd.Name);
            if (!rename.IsSuccess)
                return FeatureObjectResultModel<RenameProductTagResponse>.Error(rename.Messages);

            session.Store(tag);
            return FeatureObjectResultModel<RenameProductTagResponse>.Ok(
                new RenameProductTagResponse { Id = tag.Id, Name = tag.Name });
        }
    }
}

public static class RenameProductTagCommandEndpoint
{
    public static RouteGroupBuilder RenameProductTagGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/", async ([FromBody] RenameProductTag.RenameProductTagCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RenameProductTag.RenameProductTagResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("RenameProductTag");
        return group;
    }
}