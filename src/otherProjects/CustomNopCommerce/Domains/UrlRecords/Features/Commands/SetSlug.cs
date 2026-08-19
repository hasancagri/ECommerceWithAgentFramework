namespace CustomNopCommerce.Domains.UrlRecords.Features.Commands;

/// <summary>Bir varlığa aktif slug atayan write-slice'ı. Slug'ı normalize eder; başka aktif varlık aynı
/// slug'ı kullanıyorsa reddeder; varlığın önceki aktif slug'ını pasifleştirir (redirect geçmişi); yeni
/// aktif kaydı oluşturur. "Tek aktif slug" + "slug tekliği" invariant'ları burada (aggregate'ler arası).</summary>
public static class SetSlug
{
    public record SetSlugCommand(Guid EntityId, string EntityName, string Slug);

    public class SetSlugResponse
    {
        public Guid Id { get; set; }
        public string Slug { get; set; } = default!;
    }

    [Transactional]
    public class SetSlugCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetSlugResponse>> Handle(
            SetSlugCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.EntityName))
                return FeatureObjectResultModel<SetSlugResponse>.Error(new MessageItem
                { Property = nameof(cmd.EntityName), Code = SeoResourceConstants.ENTITY_NAME_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.Slug))
                return FeatureObjectResultModel<SetSlugResponse>.Error(new MessageItem
                { Property = nameof(cmd.Slug), Code = SeoResourceConstants.SLUG_REQUIRED });

            var normalized = cmd.Slug.Trim().ToLowerInvariant().Replace(' ', '-');

            // Aynı slug BAŞKA aktif bir varlıkta kullanılıyorsa reddet (slug tekliği).
            var taken = await session.Query<UrlRecord>()
                .Where(u => u.Slug == normalized && u.IsActive && !u.IsDeleted
                            && (u.EntityName != cmd.EntityName || u.EntityId != cmd.EntityId))
                .AnyAsync(ct);
            if (taken)
                return FeatureObjectResultModel<SetSlugResponse>.Error(new MessageItem
                { Property = nameof(cmd.Slug), Code = SeoResourceConstants.SLUG_TAKEN });

            // Bu varlığın önceki aktif slug'ını pasifleştir (redirect kaynağı olur).
            var current = await session.Query<UrlRecord>()
                .Where(u => u.EntityName == cmd.EntityName && u.EntityId == cmd.EntityId
                            && u.IsActive && !u.IsDeleted)
                .ToListAsync(ct);
            foreach (var record in current)
            {
                record.Deactivate();
                session.Update(record);
            }

            var newRecord = UrlRecord.Create(cmd.EntityId, cmd.EntityName, normalized);
            session.Store(newRecord);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<SetSlugResponse>.Ok(
                new SetSlugResponse { Id = newRecord.Id, Slug = normalized });
        }
    }
}

public static class SetSlugCommandEndpoint
{
    public static RouteGroupBuilder SetSlugGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] SetSlug.SetSlugCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SetSlug.SetSlugResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("SetSlug");
        return group;
    }
}
