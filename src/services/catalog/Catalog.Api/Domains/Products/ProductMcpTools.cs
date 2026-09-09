namespace Catalog.Api.Domains.Products;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir:
// agent'a acik her islem Agent klasorunde gorunur (kullanici karari, 005).

// 063: ürünün fiyat geçmişi (append-only ProductPriceChange, 058). Anonim (catalog MCP korumasız).
[McpServerToolType]
public static class GetPriceHistoryMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.GetPriceHistory)]
    [Description(
        "Bir urunun gecmis fiyat degisikliklerini (eski fiyat, yeni fiyat, tarih) kronolojik listeler. " +
        "productId = search_products/get_product'tan donen urun kimligi.")]
    public static Task<FeatureListResultModel<GetPriceHistoryForAgent.PriceHistoryEntry>> GetPriceHistoryAsync(
        [Description("Urun kimligi (search_products/get_product'tan)")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<GetPriceHistoryForAgent.PriceHistoryEntry>>(
            new GetPriceHistoryForAgent.GetPriceHistoryQuery(productId), ct);
}

[McpServerToolType]
public static class GetProductMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.GetProduct)]
    [Description("Sepete ekleme icin: urunu isme gore arar ve add_to_cart'a yetecek bilgiyi (id, ad, fiyat, gorsel) doner.")]
    public static Task<FeatureObjectResultModel<GetProductForAgent.GetProductResponse>> GetProductAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetProductForAgent.GetProductResponse>>(
            new GetProductForAgent.GetProductQuery(name), ct);
}

// 070: ADMIN tool'ları — YALNIZ korumalı /mcp-admin ucunda yayınlanır (Program.cs oturum filtresi);
// anonim /mcp keşif seti DEĞİŞMEZ. Kullanıcı token'dan (ICurrentUser); scope katmanı handler'da
// [RequiredScope(CatalogWrite)]. Yazma tool'ları TEK ürün işler (FR-010) + AdminActionLog izi bırakır.

[McpServerToolType]
public static class AdminListProductsMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.ListProducts)]
    [Description(
        "YONETIM: urunleri sayfali listeler — yayinda OLMAYANLAR (draft) dahil. Donen her satir: " +
        "productId (diger admin tool'larinin anahtari), name, isbn, price (TL), isPublished, " +
        "authorNames. Yanitta totalCount + page + pageSize de doner; devami icin ayni aramayla " +
        "page'i artir. Ornek: q='dune' ile ada gore ara; q bir ISBN ise tam eslesme aranir.")]
    public static Task<FeatureObjectResultModel<AdminListProductsForAgent.AdminListProductsResponse>> AdminListProductsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Sayfa numarasi (1'den baslar)")] int page = 1,
        [Description("Sayfa boyutu (1-50; varsayilan 20)")] int pageSize = AdminListProductsForAgent.DefaultPageSize,
        [Description("Arama: urun adinda gecen kelime YA DA tam ISBN; bos birakilirsa tum urunler")] string? q = null)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminListProductsForAgent.AdminListProductsResponse>>(
            new AdminListProductsForAgent.AdminListProductsQuery(page, pageSize, q), ct);
}

[McpServerToolType]
public static class AdminGetProductMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.GetProduct)]
    [Description(
        "YONETIM: tek urunun tam yonetim detayini doner (draft dahil): kunye (name, shortDescription, " +
        "fullDescription, sku, isbn, price, imageUrl), baglar (authors ad+id, publisherId+publisherName, " +
        "categoryId+categoryName), isPublished ve fiyat degisiklik gecmisi (oldPrice→newPrice, " +
        "changedAtUtc) — TEK cagrida. productId = admin_list_products'tan donen kimlik. imageUrl'i " +
        "kullaniciya TIKLANABILIR link olarak sun (markdown: [Kapak](url)).")]
    public static Task<FeatureObjectResultModel<AdminGetProductForAgent.AdminProductDetailResponse>> AdminGetProductAsync(
        [Description("Urun kimligi (admin_list_products'tan)")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetProductForAgent.AdminProductDetailResponse>>(
            new AdminGetProductForAgent.AdminGetProductQuery(productId), ct);
}

[McpServerToolType]
public static class AdminUpdateProductMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.UpdateProduct)]
    [Description(
        "YONETIM/YAZMA: TEK urunun kunyesini KISMI gunceller — yalniz verdigin alanlar degisir, " +
        "digerleri aynen kalir. Ornek: fiyati 95 yapmak icin yalniz productId + price=95 gonder. " +
        "authorIds verilirse yazar SETI onunla DEGISIR; newAuthorNames listede olmayan yazar adlarini " +
        "olusturup ekler. publisherId YA DA newPublisherName ile yayinevi degisir; categoryId ile " +
        "kategori tasinir. Fiyat degisimi fiyat gecmisine kaydolur ve vitrine yansir. Yanit urunun " +
        "GUNCEL halidir — sonucu gostermek icin ek cagri GEREKMEZ. Toplu guncelleme YOKTUR; her urun " +
        "icin ayri cagri yap. Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<AdminUpdateProductForAgent.AdminUpdateProductResponse>> AdminUpdateProductAsync(
        [Description("Guncellenecek urunun kimligi")] Guid productId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Yeni urun adi (degistirmeyeceksen bos birak)")] string? name = null,
        [Description("Yeni kisa aciklama")] string? shortDescription = null,
        [Description("Yeni tam aciklama")] string? fullDescription = null,
        [Description("Yeni fiyat (TL, >= 0); yalniz gercek degisim gecmise yazilir")] decimal? price = null,
        [Description("Yazar kimlikleri — verilirse yazar setini TAMAMEN degistirir")] List<Guid>? authorIds = null,
        [Description("Katalogda olmayan yeni yazar adlari (olusturulup eklenir)")] List<string>? newAuthorNames = null,
        [Description("Yeni yayinevi kimligi")] Guid? publisherId = null,
        [Description("Katalogda olmayan yeni yayinevi adi (olusturulur)")] string? newPublisherName = null,
        [Description("Yeni kategori kimligi (list_categories'ten)")] Guid? categoryId = null,
        [Description("Yeni kapak gorseli URL'i")] string? imageUrl = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminUpdateProductForAgent.AdminUpdateProductResponse>>(
            new AdminUpdateProductForAgent.AdminUpdateProductCommand(
                userId, productId, name, shortDescription, fullDescription, price,
                authorIds, newAuthorNames, publisherId, newPublisherName, categoryId, imageUrl), ct);
    }
}

[McpServerToolType]
public static class AdminSetPublishedMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.SetPublished)]
    [Description(
        "YONETIM/YAZMA: TEK urunu yayina alir (published=true) veya yayindan kaldirir (published=false). " +
        "Yayindan kalkan urun vitrinden/kesiften duser ama SILINMEZ — tekrar yayina alinabilir. " +
        "Fiyatsiz urun yayina ALINAMAZ (is kurali hatasi doner). Yanit guncel {productId, isPublished}. " +
        "Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<AdminSetPublishedForAgent.AdminSetPublishedResponse>> AdminSetPublishedAsync(
        [Description("Urun kimligi")] Guid productId,
        [Description("true = yayina al, false = yayindan kaldir")] bool published,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSetPublishedForAgent.AdminSetPublishedResponse>>(
            new AdminSetPublishedForAgent.AdminSetPublishedCommand(userId, productId, published), ct);
    }
}

[McpServerToolType]
public static class AdminGetPriceHistoryMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.GetPriceHistory)]
    [Description(
        "YONETIM: urunun fiyat degisiklik gecmisini kronolojik listeler (draft dahil): " +
        "[{oldPrice, newPrice, changedAtUtc}]. productId = admin_list_products/admin_get_product'tan.")]
    public static Task<FeatureListResultModel<AdminGetPriceHistoryForAgent.PriceChangeItem>> AdminGetPriceHistoryAsync(
        [Description("Urun kimligi")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<AdminGetPriceHistoryForAgent.PriceChangeItem>>(
            new AdminGetPriceHistoryForAgent.AdminGetPriceHistoryQuery(productId), ct);
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.SearchProducts)]
    [Description("Katalogda isme gore en iyi eslesen urunun productId ve adini doner (link YOK — magaza " +
                 "ekransiz). Kategori ve/veya yazar adiyla daraltilabilir.")]
    public static Task<FeatureObjectResultModel<SearchProductsForAgent.SearchProductResponse>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Opsiyonel kategori adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? category = null,
        [Description("Opsiyonel yazar adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? author = null)
        => bus.InvokeAsync<FeatureObjectResultModel<SearchProductsForAgent.SearchProductResponse>>(
            new SearchProductsForAgent.SearchProductsQuery(name, category, author), ct);
}