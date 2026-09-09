
namespace Customer.Api.Domains.Wallets;

// MCP okuma-yalniz: KART EKLEME ASLA bir tool DEGIL (ham PAN LLM turuna girmez, FR-019).
// Sil/varsayilan-yap da tool degil — yalniz REST/WebApp. Burada yalniz guvenli listeleme.
[McpServerToolType]
public static class ListCardsMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.ListCards)]
    [Description("Giris yapmis kullanicinin kayitli kartlarini listeler (yalniz marka + son 4 hane + son-kullanma + etiket; PAN/CVV/token asla).")]
    public static Task<FeatureListResultModel<GetCardsForAgent.CardView>> ListCardsAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<GetCardsForAgent.CardView>>(
            new GetCardsForAgent.GetCardsQuery(userId), ct);
    }
}

// 070-sonrasi guvenlik sokumu: get_default_card_bin + get_payment_context tool'lari KALDIRILDI —
// odeme baglami (vault token + buyer) yalniz S2S internal REST'te (GetPaymentContextForAgent slice
// DURUYOR, tuketicisi Order.Api). Taksit sorgusu dis yuzeyde quote_installments (Order /mcp).
