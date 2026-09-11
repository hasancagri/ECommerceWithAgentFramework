namespace Catalog.Api.Domains.SpecificationAttributes;

using Features.Agents;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
// 074: özellik tanımı admin parite tool'ları — YALNIZ korumalı /mcp-admin ucunda yayınlanır
// (anonim /mcp keşif seti DEĞİŞMEZ). Yazma tool'ları kullanıcı token'dan (ICurrentUser); scope
// katmanı handler'da [RequiredScope(CatalogWrite)] + AdminActionLog izi. Okuma tool'u scope/iz YOK.
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).

[McpServerToolType]
public static class AdminCreateSpecificationAttributeMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateSpecificationAttribute)]
    [Description(
        "YONETIM/YAZMA: yeni bir kanonik ozellik tanimi olusturur (ornek: 'Renk', 'Materyal'). " +
        "filterable=true ise vitrin facet filtresine girer. Ayni isimli tanim varsa hata doner. " +
        "Deger listesini SONRA admin_add_specification_attribute_option ile eklersin. Yanit yeni " +
        "tanimin id'sidir. Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<AdminCreateSpecificationAttributeForAgent.AdminCreateSpecificationAttributeResponse>> AdminCreateSpecificationAttributeAsync(
        [Description("Ozellik tanimi adi (ornek: Renk)")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Vitrin facet filtresine girsin mi (varsayilan false)")] bool filterable = false,
        [Description("Gorunum sirasi (kucuk once; varsayilan 0)")] int displayOrder = 0)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminCreateSpecificationAttributeForAgent.AdminCreateSpecificationAttributeResponse>>(
            new AdminCreateSpecificationAttributeForAgent.AdminCreateSpecificationAttributeCommand(
                userId, name, filterable, displayOrder), ct);
    }
}

[McpServerToolType]
public static class AdminAddSpecificationAttributeOptionMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.AddSpecificationAttributeOption)]
    [Description(
        "YONETIM/YAZMA: var olan bir ozellik tanimina kapalı-liste degeri (secenek) ekler " +
        "(ornek: Renk tanimina 'Siyah'). attributeId = admin_list_specification_attributes'tan donen " +
        "tanim kimligi. Ayni isimli secenek varsa hata doner. Yanit yeni secenegin optionId'sidir. " +
        "Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<AdminAddSpecificationAttributeOptionForAgent.AdminAddSpecificationAttributeOptionResponse>> AdminAddSpecificationAttributeOptionAsync(
        [Description("Ozellik tanimi kimligi (admin_list_specification_attributes'tan)")] Guid attributeId,
        [Description("Yeni secenek adi (ornek: Siyah)")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Gorunum sirasi (kucuk once; varsayilan 0)")] int displayOrder = 0)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminAddSpecificationAttributeOptionForAgent.AdminAddSpecificationAttributeOptionResponse>>(
            new AdminAddSpecificationAttributeOptionForAgent.AdminAddSpecificationAttributeOptionCommand(
                userId, attributeId, name, displayOrder), ct);
    }
}

[McpServerToolType]
public static class AdminListSpecificationAttributesMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.ListSpecificationAttributes)]
    [Description(
        "YONETIM: tum ozellik tanimlarini kapalı-liste secenekleriyle birlikte listeler. Donen her " +
        "satir: id (diger tool'larin anahtari), name, filterable, displayOrder ve options ([{id, name, " +
        "displayOrder}]). Yeni secenek eklemeden once tanim id'sini buradan al.")]
    public static Task<FeatureListResultModel<AdminListSpecificationAttributesForAgent.SpecificationAttributeItem>> AdminListSpecificationAttributesAsync(
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<AdminListSpecificationAttributesForAgent.SpecificationAttributeItem>>(
            new AdminListSpecificationAttributesForAgent.ListSpecificationAttributesQuery(), ct);
}
