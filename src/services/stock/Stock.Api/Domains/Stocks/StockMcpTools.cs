namespace Stock.Api.Domains.Stocks;

[McpServerToolType]
public static class GetStockMcpTool
{
    [McpServerTool(Name = Shared.StockTools.GetStock)]
    [Description("Bir urunun stok durumunu (adet) doner; urun Id'si ile sorgular.")]
    public static Task<FeatureObjectResultModel<GetStockByProductIdForAgent.GetStockResponse>> GetStockAsync(
        [Description("Stok durumu sorgulanacak urunun Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetStockByProductIdForAgent.GetStockResponse>>(
            new GetStockByProductIdForAgent.GetStockByProductIdQuery(productId), ct);
}

// 070: ADMIN tool'ları — YALNIZ korumalı /mcp-admin ucunda yayınlanır (Program.cs oturum filtresi);
// anonim /mcp get_stock DEĞİŞMEZ. Kullanıcı token'dan; scope katmanı handler'da
// [RequiredScope(StockWrite)]. Yazma TEK ürün işler (FR-010).

[McpServerToolType]
public static class AdminListAllStockMcpTool
{
    [McpServerTool(Name = Shared.StockAdminTools.ListAllStock)]
    [Description(
        "YONETIM/OKUMA: tum urunlerin stok (OnHand) genel gorunumu — {productId, onHand} listesi. " +
        "Opsiyonel sayfalama: page (1'den baslar) + pageSize; ikisi de verilmezse tum kayitlar doner. " +
        "Salt-okuma, denetim izi birakmaz. Tek urun icin get_stock kullan.")]
    public static Task<FeatureListResultModel<AdminListAllStockForAgent.StockItemResponse>> AdminListAllStockAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Sayfa numarasi (1'den baslar); verilmezse sayfalama yok")] int? page = null,
        [Description("Sayfa basina kayit; verilmezse sayfalama yok")] int? pageSize = null)
        => bus.InvokeAsync<FeatureListResultModel<AdminListAllStockForAgent.StockItemResponse>>(
            new AdminListAllStockForAgent.AdminListAllStockQuery(page, pageSize), ct);
}

[McpServerToolType]
public static class AdminSetStockMcpTool
{
    [McpServerTool(Name = Shared.StockAdminTools.SetStock)]
    [Description(
        "YONETIM/YAZMA: TEK urunun stogunu MUTLAK degere ayarlar (ornek: 'stok 25 olsun' → quantity=25). " +
        "quantity >= 0 olmali; negatif deger is kurali hatasiyla reddedilir. Artir/azalt icin " +
        "admin_adjust_stock kullan. Yanit guncel {productId, onHand}. Urunun stok kaydi yoksa " +
        "bulunamadi doner (stok kaydi urun yayinlanirken acilir). Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<AdminSetStockForAgent.AdminSetStockResponse>> AdminSetStockAsync(
        [Description("Urun kimligi (katalogdaki productId)")] Guid productId,
        [Description("Yeni mutlak stok adedi (>= 0)")] int quantity,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSetStockForAgent.AdminSetStockResponse>>(
            new AdminSetStockForAgent.AdminSetStockCommand(userId, productId, quantity), ct);
    }
}

[McpServerToolType]
public static class AdminAdjustStockMcpTool
{
    [McpServerTool(Name = Shared.StockAdminTools.AdjustStock)]
    [Description(
        "YONETIM/YAZMA: TEK urunun stogunu delta kadar oynatir — pozitif delta artirir (ornek: " +
        "'3 ekle' → delta=3), negatif delta azaltir (ornek: '3 azalt' → delta=-3). Stogu sifirin " +
        "altina dusurecek delta is kurali hatasiyla reddedilir; delta=0 gecersizdir. Mutlak deger " +
        "icin admin_set_stock kullan. Yanit guncel {productId, onHand}. Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<AdminAdjustStockForAgent.AdminAdjustStockResponse>> AdminAdjustStockAsync(
        [Description("Urun kimligi (katalogdaki productId)")] Guid productId,
        [Description("Stok degisimi: pozitif = artir, negatif = azalt (sifir olamaz)")] int delta,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminAdjustStockForAgent.AdminAdjustStockResponse>>(
            new AdminAdjustStockForAgent.AdminAdjustStockCommand(userId, productId, delta), ct);
    }
}
