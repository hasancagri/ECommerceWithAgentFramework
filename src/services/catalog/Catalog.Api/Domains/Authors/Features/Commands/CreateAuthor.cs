namespace Catalog.Api.Domains.Authors.Features.Commands;

public static class CreateAuthor
{
    public record CreateAuthorCommand(string Name);

    public class CreateAuthorResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateAuthorCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateAuthorResponse>> Handle(
            CreateAuthorCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // REST yaratımı bilinçlidir: aynı ad varsa açık hata (import yolu get-or-create Upsert'te).
            var normalized = NameNormalization.Normalize(cmd.Name ?? string.Empty);
            var exists = await session.Query<Author>()
                .AnyAsync(x => x.NormalizedName == normalized && !x.IsDeleted, ct);
            if (exists)
                return FeatureObjectResultModel<CreateAuthorResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.AUTHOR_ALREADY_EXISTS
                });

            var created = Author.Create(cmd.Name!);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreateAuthorResponse>.Error(created.Messages);

            session.Store(created.Data!);
            return FeatureObjectResultModel<CreateAuthorResponse>.Ok(
                new CreateAuthorResponse { Id = created.Data!.Id });
        }
    }
}

public static class CreateAuthorCommandEndpoint
{
    public static RouteGroupBuilder CreateAuthorGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateAuthor.CreateAuthorCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateAuthor.CreateAuthorResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateAuthor");
        return group;
    }
}