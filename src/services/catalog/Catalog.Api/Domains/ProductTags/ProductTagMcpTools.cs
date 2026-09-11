namespace Catalog.Api.Domains.ProductTags;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
// 070: ADMIN tool'lari — YALNIZ korumali /mcp-admin ucunda yayinlanir (Program.cs oturum filtresi);
// anonim /mcp kesif seti DEGISMEZ. Kullanici token'dan (ICurrentUser); scope katmani handler'da
// [RequiredScope(CatalogWrite)]. Yazma tool'lari AdminActionLog izi birakir.
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).

[McpServerToolType]
public static class AdminCreateProductTagMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateProductTag)]
    [Description(
        "YONETIM/YAZMA: yeni bir urun etiketi olusturur (or. 'yeni-sezon', 'outlet'). Ad zorunludur. " +
        "Yanit {id, name} — olusturulan etiketin kimligi ve adi. Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<Features.Agents.AdminCreateProductTagForAgent.AdminCreateProductTagResponse>> AdminCreateProductTagAsync(
        [Description("Yeni etiket adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.AdminCreateProductTagForAgent.AdminCreateProductTagResponse>>(
            new Features.Agents.AdminCreateProductTagForAgent.AdminCreateProductTagCommand(userId, name), ct);
    }
}

[McpServerToolType]
public static class AdminRenameProductTagMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.RenameProductTag)]
    [Description(
        "YONETIM/YAZMA: mevcut bir urun etiketinin adini degistirir. tagId = admin_list_product_tags'ten " +
        "donen kimlik. Ad zorunludur. Yanit {id, name} — guncel hali. Etiket bulunamazsa hata doner. " +
        "Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<Features.Agents.AdminRenameProductTagForAgent.AdminRenameProductTagResponse>> AdminRenameProductTagAsync(
        [Description("Etiket kimligi (admin_list_product_tags'ten)")] Guid tagId,
        [Description("Yeni etiket adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.AdminRenameProductTagForAgent.AdminRenameProductTagResponse>>(
            new Features.Agents.AdminRenameProductTagForAgent.AdminRenameProductTagCommand(userId, tagId, name), ct);
    }
}

[McpServerToolType]
public static class AdminListProductTagsMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.ListProductTags)]
    [Description(
        "YONETIM: urun etiketlerini ada gore sirali listeler. Donen her satir {id, name}; id, " +
        "admin_rename_product_tag'in anahtaridir. search ile etiket adinda gecen metne gore daraltilabilir.")]
    public static Task<FeatureListResultModel<Features.Agents.AdminListProductTagsForAgent.ProductTagItem>> AdminListProductTagsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Etiket adinda gecen metin (bos = tumu)")] string? search = null)
        => bus.InvokeAsync<FeatureListResultModel<Features.Agents.AdminListProductTagsForAgent.ProductTagItem>>(
            new Features.Agents.AdminListProductTagsForAgent.ListProductTagsQuery(search), ct);
}
