namespace Catalog.Api.Domains.Authors;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).
[McpServerToolType]
public static class ListAuthorsMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.ListAuthors)]
    [Description("Magazadaki yazarlari listeler (yalniz yayinda kitabi olanlar), kitap sayisi cok olan " +
                 "once. totalCount toplam yazar sayisidir; liste kirpilmis olabilir — daraltmak icin " +
                 "search ile ada gore filtrele.")]
    public static Task<FeatureObjectResultModel<Features.Agents.ListAuthorsForAgent.ListAuthorsResponse>> ListAuthorsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yazar adinda gecen metin (bos = tumu)")] string? search = null,
        [Description("Sonuc sayisi; varsayilan 50, en fazla 200")] int? maxResults = null)
        => bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.ListAuthorsForAgent.ListAuthorsResponse>>(
            new Features.Agents.ListAuthorsForAgent.ListAuthorsQuery(search, maxResults), ct);
}

// 074: ADMIN tool — YALNIZ korumalı /mcp-admin ucunda yayınlanır (anonim /mcp keşif seti DEĞİŞMEZ).
// Kullanıcı token'dan (ICurrentUser); scope katmanı handler'da [RequiredScope(CatalogWrite)].
[McpServerToolType]
public static class AdminCreateAuthorMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateAuthor)]
    [Description(
        "YONETIM/YAZMA: bir yazar kaydi olusturur. Ayni ad zaten varsa YENISI olusturulmaz — mevcut " +
        "yazar dondurulur (idempotent get-or-create), boylece urun bagi kurmadan once yazar kimligini " +
        "guvenle alabilirsin. Yanit {id, name}. Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<Features.Agents.AdminCreateAuthorForAgent.AdminCreateAuthorResponse>> AdminCreateAuthorAsync(
        [Description("Yazar adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.AdminCreateAuthorForAgent.AdminCreateAuthorResponse>>(
            new Features.Agents.AdminCreateAuthorForAgent.AdminCreateAuthorCommand(userId, name), ct);
    }
}