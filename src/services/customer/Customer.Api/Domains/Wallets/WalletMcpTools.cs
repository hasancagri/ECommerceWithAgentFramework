
namespace Customer.Api.Domains.Wallets;

// MCP okuma-yalniz: KART EKLEME ASLA bir tool DEGIL (ham PAN LLM turuna girmez, FR-019).
// Sil/varsayilan-yap da tool degil — yalniz REST/WebApp. Burada yalniz guvenli listeleme.
[McpServerToolType]
public static class ListCardsMcpTool
{
    [McpServerTool(Name = "list_cards")]
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

// 024: taksit sorgusu icin default kartin BIN'ini (ilk 6 hane) veren okuma tool'u. HASSAS DEGIL.
// PAN/CVV/token ASLA. Default kart yoksa NotFound (assistant BIN'siz genel sorgu / kart ekle ister).
[McpServerToolType]
public static class DefaultCardBinMcpTool
{
    [McpServerTool(Name = "get_default_card_bin")]
    [Description("Kullanicinin varsayilan kartinin BIN'ini (ilk 6 hane, banka tespiti icin) + marka + son 4 hane doner. Taksit sorgusunda kullanilir. PAN/CVV/token asla donmez. Varsayilan kart yoksa bulunamaz.")]
    public static Task<FeatureObjectResultModel<GetDefaultCardBinForAgent.DefaultCardBinView>> GetDefaultCardBinAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetDefaultCardBinForAgent.DefaultCardBinView>>(
            new GetDefaultCardBinForAgent.GetDefaultCardBinQuery(userId), ct);
    }
}

// 038: odeme baglami MCP yuzeyi (assistant persona). 033'un get_card_installments +
// charge_default_card tool'lari ve gateway HTTP koprusu SOKULDU (tek yol = A2A): taksit sorgusu
// ve cekim artik ChatAgent -> A2A -> PaymentGateway Payment.Agent -> gateway /mcp zinciriyle
// yurur. Bu yuzey yalniz CEKIM BAGLAMINI verir: kart vault token'i (varsayilan veya secilen) +
// gercek buyer bilgisi (profil + varsayilan adres). ChatAgent bu ciktiyi A2A istegine VERBATIM
// koyar. Kart EKLEME/SILME bu yuzeyde YOK (guvenlik karari — yalniz ekran yolu).
[McpServerToolType]
public static class PaymentContextMcpTool
{
    [McpServerTool(Name = "get_payment_context")]
    [Description("Odeme (taksit sorgusu/cekim) icin baglami doner: gateway merchantId + secilen ya da " +
                 "varsayilan kartin vault token'i + alici (buyer) bilgisi (ad, soyad, e-posta, GSM, " +
                 "kayitli varsayilan adres). cardId verilirse o kart, verilmezse varsayilan kart secilir. " +
                 "Varsayilan adres ya da merchant kaydi yoksa bulunamaz doner. PAN/CVV asla donmez. Bu " +
                 "bilgi (merchantId + token dahil) odeme istegine OLDUGU GIBI tasinir; kullaniciya gosterilmez.")]
    public static Task<FeatureObjectResultModel<GetPaymentContextForAgent.PaymentContextView>> GetPaymentContextAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Secilen kartin Id'si (list_cards'tan); verilmezse varsayilan kart")] Guid? cardId = null)
    {
        var user = currentUser.Load(http.HttpContext!.User);
        return bus.InvokeAsync<FeatureObjectResultModel<GetPaymentContextForAgent.PaymentContextView>>(
            new GetPaymentContextForAgent.GetPaymentContextQuery(
                user.Id, user.Name, user.Email, user.Phone, cardId), ct);
    }
}