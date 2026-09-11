
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

// GÜVENLİK SÖKÜMÜ (fix/payment-context-mcp-removal): get_default_card_bin + get_payment_context MCP
// tool'ları KALDIRILDI — vault token + buyer PII'yi (TCKN dahil) dış agent'ın sohbet bağlamına
// sızdırıyorlardı (038 ChatAgent-sunucu varsayımıyla; 061/073 sonrası dış istemci de görüyordu).
// Ödeme bağlamı yalnız S2S internal REST'te (GetPaymentContextForAgent slice DURUYOR, tüketici Order.Api);
// taksit sorgusu dış yüzeyde quote_installments (Order /mcp). Sohbet bandına ödeme sırrı taşıyan tool yok.