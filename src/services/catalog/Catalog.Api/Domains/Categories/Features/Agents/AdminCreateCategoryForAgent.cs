namespace Catalog.Api.Domains.Categories.Features.Agents;

// 074: CreateCategory REST Command'ının agent İKİZİ (bilinçli tekrar) — MCP-only iş yüzeyi.
// Yaratım bilinçlidir: aynı ad varsa get-or-create DEĞİL, açık hata (feed yolu Upsert'te).
// Yazılan kategori vitrindedir (K8). İz: AdminActionLog (FR-009); iş-kuralı reddinde Rejected satırı düşer.
public static class AdminCreateCategoryForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminCreateCategoryCommand(
        Guid UserId,
        string Name,
        string? Description,
        Guid? ParentId,
        int DisplayOrder,
        string? MetaTitle,
        string? MetaKeywords,
        string? MetaDescription);

    public class AdminCreateCategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminCreateCategoryForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateCategoryResponse>> Handle(
            AdminCreateCategoryCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(cmd.Name ?? string.Empty);
            var exists = await session.Query<Category>()
                .AnyAsync(x => x.NormalizedName == normalized && !x.IsDeleted, ct);
            if (exists)
            {
                session.Store(AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.CreateCategory, "-", "category already exists"));
                return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.CATEGORY_ALREADY_EXISTS
                });
            }

            if (cmd.ParentId is not null)
            {
                var parent = await session.LoadAsync<Category>(cmd.ParentId.Value, ct);
                if (parent is null || parent.IsDeleted)
                {
                    session.Store(AdminActionLog.Rejected(
                        cmd.UserId, CatalogAdminTools.CreateCategory, "-", "parent category not found"));
                    return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(new MessageItem
                    {
                        Property = nameof(cmd.ParentId),
                        Code = CatalogResourceConstants.RECORD_NOT_FOUND
                    });
                }
            }

            var created = Category.Create(cmd.Name!, cmd.Description ?? string.Empty, cmd.ParentId, cmd.DisplayOrder);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(created.Messages);

            var category = created.Data!;

            // K8 düzeni: yazılan kategori vitrindedir.
            var publish = category.SetPublished(true);
            if (!publish.IsSuccess)
                return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(publish.Messages);

            if (cmd.MetaTitle is not null || cmd.MetaKeywords is not null || cmd.MetaDescription is not null)
            {
                var seo = category.SetSeo(SeoMetadata.Create(cmd.MetaTitle, cmd.MetaKeywords, cmd.MetaDescription));
                if (!seo.IsSuccess)
                    return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(seo.Messages);
            }

            session.Store(category);
            session.Store(AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.CreateCategory, category.Id.ToString(), $"created '{category.Name}'"));

            return FeatureObjectResultModel<AdminCreateCategoryResponse>.Ok(new AdminCreateCategoryResponse
            {
                Id = category.Id,
                Name = category.Name
            });
        }
    }
}
