namespace Catalog.Api.Domains.Authors.Features.Agents;

// 074: CreateAuthor agent yüzeyi — MCP-only iş yüzeyi. REST Command'ı aynı ad varsa AÇIK hata döner;
// agent yolu ise ImportBook.GetOrCreateAuthors mantığını izler: aynı NormalizedName varsa MEVCUT
// yazarı döndürür (idempotent, agent yeni yazar oluşturmadan bağ kurabilsin). İz: AdminActionLog.
public static class AdminCreateAuthorForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminCreateAuthorCommand(Guid UserId, string Name);

    public class AdminCreateAuthorResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminCreateAuthorForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateAuthorResponse>> Handle(
            AdminCreateAuthorCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<AdminCreateAuthorResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.VALUE_EMPTY
                });

            // get-or-create: aynı normalize ad varsa mevcut yazarı döndür (ImportBook deseni).
            var normalized = NameNormalization.Normalize(cmd.Name);
            var existing = await session.Query<Author>()
                .FirstOrDefaultAsync(a => a.NormalizedName == normalized && !a.IsDeleted, ct);
            if (existing is not null)
            {
                session.Store(AdminActionLog.Executed(
                    cmd.UserId, CatalogAdminTools.CreateAuthor, existing.Id.ToString(), $"existing '{existing.Name}'"));
                return FeatureObjectResultModel<AdminCreateAuthorResponse>.Ok(new AdminCreateAuthorResponse
                {
                    Id = existing.Id,
                    Name = existing.Name
                });
            }

            var created = Author.Create(cmd.Name);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<AdminCreateAuthorResponse>.Error(created.Messages);

            var author = created.Data!;
            session.Store(author);
            session.Store(AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.CreateAuthor, author.Id.ToString(), $"created '{author.Name}'"));

            return FeatureObjectResultModel<AdminCreateAuthorResponse>.Ok(new AdminCreateAuthorResponse
            {
                Id = author.Id,
                Name = author.Name
            });
        }
    }
}
