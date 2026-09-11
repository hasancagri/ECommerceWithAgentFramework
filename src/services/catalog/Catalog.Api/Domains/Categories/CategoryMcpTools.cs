namespace Catalog.Api.Domains.Categories;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
[McpServerToolType]
public static class ListCategoriesMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.ListCategories)]
    [Description("Magazadaki kategorileri listeler (yalniz yayinda urunu olan kategoriler). Her kategori " +
                 "ad, ust kategori (parentCategory, varsa) ve urun sayisi (productCount) tasir. 'Hangi " +
                 "kategoriler var' tarzi kesif sorulari icin.")]
    public static Task<FeatureListResultModel<Features.Agents.ListCategoriesForAgent.CategoryItem>> ListCategoriesAsync(
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<Features.Agents.ListCategoriesForAgent.CategoryItem>>(
            new Features.Agents.ListCategoriesForAgent.ListCategoriesQuery(), ct);
}

// 074: ADMIN tool'ları — YALNIZ korumalı /mcp-admin ucunda yayınlanır (anonim /mcp keşif seti DEĞİŞMEZ).
// Kullanıcı token'dan (ICurrentUser); scope katmanı handler'da [RequiredScope(CatalogWrite)].
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).

[McpServerToolType]
public static class AdminCreateCategoryMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateCategory)]
    [Description(
        "YONETIM/YAZMA: yeni bir kategori olusturur (vitrinde yayinda dogar). Ayni ad zaten varsa " +
        "HATA doner (get-or-create DEGIL) — once list_categories ile kontrol et. parentId ile ust " +
        "kategoriye baglanir (bulunamazsa hata). SEO alanlari opsiyoneldir. Yanit {id, name}. " +
        "Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<Features.Agents.AdminCreateCategoryForAgent.AdminCreateCategoryResponse>> AdminCreateCategoryAsync(
        [Description("Kategori adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Kategori aciklamasi (opsiyonel)")] string? description = null,
        [Description("Ust kategori kimligi (list_categories'ten; kok icin bos birak)")] Guid? parentId = null,
        [Description("Vitrin siralama degeri (varsayilan 0)")] int displayOrder = 0,
        [Description("SEO meta baslik (opsiyonel)")] string? metaTitle = null,
        [Description("SEO meta anahtar kelimeler (opsiyonel)")] string? metaKeywords = null,
        [Description("SEO meta aciklama (opsiyonel)")] string? metaDescription = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.AdminCreateCategoryForAgent.AdminCreateCategoryResponse>>(
            new Features.Agents.AdminCreateCategoryForAgent.AdminCreateCategoryCommand(
                userId, name, description, parentId, displayOrder, metaTitle, metaKeywords, metaDescription), ct);
    }
}

[McpServerToolType]
public static class AdminUpdateCategoryMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.UpdateCategory)]
    [Description(
        "YONETIM/YAZMA: TEK kategoriyi KISMI gunceller — yalniz verdigin alanlar degisir, digerleri " +
        "aynen kalir. Ornek: yalniz categoryId + name gonder. Guncellenebilir alanlar: ad ve SEO meta " +
        "(baslik/anahtar/aciklama; verilen SEO alani ustune yazilir, verilmeyen mevcut kalir). Yanit " +
        "kategorinin GUNCEL halidir. Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<Features.Agents.AdminUpdateCategoryForAgent.AdminUpdateCategoryResponse>> AdminUpdateCategoryAsync(
        [Description("Guncellenecek kategori kimligi (list_categories'ten)")] Guid categoryId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Yeni kategori adi (degistirmeyeceksen bos birak)")] string? name = null,
        [Description("Yeni SEO meta baslik")] string? metaTitle = null,
        [Description("Yeni SEO meta anahtar kelimeler")] string? metaKeywords = null,
        [Description("Yeni SEO meta aciklama")] string? metaDescription = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.AdminUpdateCategoryForAgent.AdminUpdateCategoryResponse>>(
            new Features.Agents.AdminUpdateCategoryForAgent.AdminUpdateCategoryCommand(
                userId, categoryId, name, metaTitle, metaKeywords, metaDescription), ct);
    }
}