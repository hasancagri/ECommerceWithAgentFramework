namespace Catalog.Api.Domains.Categories.Features.Agents;

// 074: UpdateCategory REST Command'ının agent İKİZİ (bilinçli tekrar) ama KISMİ güncelleme
// (AdminUpdateProduct deseni): agent "adını X yap" der, tüm formu göndermez — yalnız verilen alan değişir.
// İz: AdminActionLog (FR-009); bulunamadı da iz bırakır (Rejected satırı).
public static class AdminUpdateCategoryForAgent
{
    // NOT: Category aggregate'i yalnız Rename + SetSeo mutasyonu sunar (Description Create'te sabitlenir,
    // mutator yok). Bu yüzden kısmi güncelleme yüzeyi ad + SEO ile sınırlı — dead param eklenmez.
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminUpdateCategoryCommand(
        Guid UserId,
        Guid CategoryId,
        string? Name,
        string? MetaTitle,
        string? MetaKeywords,
        string? MetaDescription);

    public class AdminUpdateCategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
    }

    [Transactional]
    public class AdminUpdateCategoryForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminUpdateCategoryResponse>> Handle(
            AdminUpdateCategoryCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var category = await session.LoadAsync<Category>(cmd.CategoryId, ct);
            if (category is null || category.IsDeleted)
            {
                session.Store(AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.UpdateCategory, cmd.CategoryId.ToString(), "category not found"));
                return FeatureObjectResultModel<AdminUpdateCategoryResponse>.NotFound();
            }

            var changed = new List<string>();

            if (!string.IsNullOrWhiteSpace(cmd.Name) && cmd.Name != category.Name)
            {
                var rename = category.Rename(cmd.Name);
                if (!rename.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateCategoryResponse>.Error(rename.Messages);
                changed.Add("Name");
            }

            // SEO KISMİ: verilen alanlar üzerine yazılır, verilmeyenler mevcut değerini korur.
            if (cmd.MetaTitle is not null || cmd.MetaKeywords is not null || cmd.MetaDescription is not null)
            {
                var seo = category.SetSeo(SeoMetadata.Create(
                    cmd.MetaTitle ?? category.Seo.MetaTitle,
                    cmd.MetaKeywords ?? category.Seo.MetaKeywords,
                    cmd.MetaDescription ?? category.Seo.MetaDescription));
                if (!seo.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateCategoryResponse>.Error(seo.Messages);
                changed.Add("Seo");
            }

            session.Store(category);
            session.Store(AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.UpdateCategory, category.Id.ToString(),
                changed.Count > 0 ? string.Join("; ", changed) : "no-op"));

            return FeatureObjectResultModel<AdminUpdateCategoryResponse>.Ok(new AdminUpdateCategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            });
        }
    }
}
