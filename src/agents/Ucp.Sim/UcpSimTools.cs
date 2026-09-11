namespace Ucp.Sim;

// UCP platform simülatörünün MCP tool'ları (Claude Desktop dış AI platform rolüyle sürer). Her tool
// mağazanın /ucp checkout cephesini client_credentials token'ıyla çağırır, ham yanıtı LLM'e döner.
// Optional param'lara DEFAULT şart ([[mcp-tool-optional-param-default]]) — yoksa omit'te hata.

[McpServerToolType]
public static class UcpCreateSessionTool
{
    [McpServerTool(Name = "ucp_create_session")]
    [Description("Magazada yeni bir UCP checkout session acar. productId (Guid) + quantity zorunlu; " +
        "buyer alanlari opsiyonel. Yanit session govdesi (id/status/totals) — 'id'yi sonraki cagrilarda kullan.")]
    public static Task<string> CreateAsync(
        UcpStoreClient store,
        string productId,
        int quantity = 1,
        string currency = "TRY",
        string buyerEmail = "",
        string firstName = "",
        string lastName = "",
        CancellationToken ct = default)
    {
        object? buyer = string.IsNullOrWhiteSpace(buyerEmail)
            ? null
            : new { email = buyerEmail, firstName, lastName };
        var body = new
        {
            currency,
            lineItems = new[] { new { productId, quantity } },
            buyer
        };
        return store.CreateSessionAsync(body, ct);
    }
}

[McpServerToolType]
public static class UcpUpdateSessionTool
{
    [McpServerTool(Name = "ucp_update_session")]
    [Description("Session'i gunceller: alici (buyer), kargo secimi (fulfillmentOptionId: standard|express + " +
        "destinationId) ve indirim kodu (discountCode) eklenebilir. Kalem degistirmek icin productId+quantity ver " +
        "(TAM degisim). Yanit guncel session; hazirsa status=ready_for_complete.")]
    public static Task<string> UpdateAsync(
        UcpStoreClient store,
        string sessionId,
        string buyerEmail = "",
        string firstName = "",
        string lastName = "",
        string fulfillmentOptionId = "",
        string destinationId = "",
        string discountCode = "",
        string productId = "",
        int quantity = 0,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(buyerEmail))
            body["buyer"] = new { email = buyerEmail, firstName, lastName };
        if (!string.IsNullOrWhiteSpace(fulfillmentOptionId))
            body["fulfillment"] = new { selectedOptionId = fulfillmentOptionId, destinationId };
        if (!string.IsNullOrWhiteSpace(discountCode))
            body["discountCodes"] = new[] { discountCode };
        if (!string.IsNullOrWhiteSpace(productId) && quantity > 0)
            body["lineItems"] = new[] { new { productId, quantity } };

        return store.UpdateSessionAsync(sessionId, body, ct);
    }
}

[McpServerToolType]
public static class UcpGetSessionTool
{
    [McpServerTool(Name = "ucp_get_session")]
    [Description("Session'in guncel durumunu getirir (id/status/totals/order).")]
    public static Task<string> GetAsync(UcpStoreClient store, string sessionId, CancellationToken ct = default)
        => store.GetSessionAsync(sessionId, ct);
}

[McpServerToolType]
public static class UcpCompleteSessionTool
{
    [McpServerTool(Name = "ucp_complete_session")]
    [Description("Session'i tamamlar (odeme tahsil + siparis). idempotencyKey verilmezse sessionId kullanilir " +
        "(ayni session tekrar complete = yeni siparis YOK). Yanitta status=completed + order referansi beklenir.")]
    public static Task<string> CompleteAsync(
        UcpStoreClient store, string sessionId, string idempotencyKey = "", CancellationToken ct = default)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? sessionId : idempotencyKey;
        return store.CompleteSessionAsync(sessionId, key, ct);
    }
}

[McpServerToolType]
public static class UcpCancelSessionTool
{
    [McpServerTool(Name = "ucp_cancel_session")]
    [Description("Terminal-olmayan bir session'i iptal eder (status=canceled). reason opsiyonel.")]
    public static Task<string> CancelAsync(
        UcpStoreClient store, string sessionId, string reason = "", CancellationToken ct = default)
        => store.CancelSessionAsync(sessionId, string.IsNullOrWhiteSpace(reason) ? null : reason, ct);
}

[McpServerToolType]
public static class UcpDiscoverTool
{
    [McpServerTool(Name = "ucp_discover")]
    [Description("Magazanin UCP kesif profilini getirir: desteklenen yetenekler/uzantilar, kabul edilen " +
        "odeme yontemleri (payment_handlers) ve public imza anahtarlari (JWKS).")]
    public static Task<string> DiscoverAsync(UcpStoreClient store, CancellationToken ct = default)
        => store.DiscoverAsync(ct);
}

[McpServerToolType]
public static class UcpSearchCatalogTool
{
    [McpServerTool(Name = "ucp_search_catalog")]
    [Description("Magazada satilabilir urunleri arar (q = anahtar kelime; baslik/yazar). En cok 20 sonuc " +
        "(id/title/price/available). Bulunan bir urunun 'id'sini ucp_create_session'da kullan.")]
    public static Task<string> SearchAsync(UcpStoreClient store, string q = "", CancellationToken ct = default)
        => store.SearchCatalogAsync(q, ct);
}

[McpServerToolType]
public static class UcpLookupProductTool
{
    [McpServerTool(Name = "ucp_lookup_product")]
    [Description("Bir urunu kimligiyle (productId) getirir: title/price/available.")]
    public static Task<string> LookupAsync(UcpStoreClient store, string productId, CancellationToken ct = default)
        => store.LookupProductAsync(productId, ct);
}