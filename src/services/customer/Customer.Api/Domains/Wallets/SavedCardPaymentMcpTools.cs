using Customer.Api.Domains.Wallets.Features.Agents;

namespace Customer.Api.Domains.Wallets;

// 033: kayitli kartla odeme MCP yuzeyi (assistant persona). get_card_installments = BILGI (default
// kart + tutar -> taksit secenekleri, cekim YAPMAZ); charge_default_card = ISLEM (GERCEK cekim,
// yalniz kullanici onayindan sonra). Vault token/PAN/CVV LLM'e ASLA donmez; yalniz taksit/tutar +
// paymentId. Kart cozumu + gateway cagrisi Customer.Api'de (token dis dunyaya sizmaz).
[McpServerToolType]
public static class CardInstallmentsMcpTool
{
    [McpServerTool(Name = "get_card_installments")]
    [Description("Kullanicinin varsayilan kayitli karti ve verilen tutar icin taksit seceneklerini (taksit sayisi + toplam odenecek tutar) doner. Odeme oncesi BILGIdir, cekim yapmaz. Varsayilan kart yoksa veya kartin BIN'i yoksa bulunamaz.")]
    public static Task<FeatureObjectResultModel<GetCardInstallmentsForAgent.InstallmentOptionsView>> GetCardInstallmentsAsync(
        [Description("Odenecek tutar (sepet toplami), TRY. Ornek: 1500.00")] decimal amount,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetCardInstallmentsForAgent.InstallmentOptionsView>>(
            new GetCardInstallmentsForAgent.GetCardInstallmentsQuery(userId, amount), ct);
    }
}

[McpServerToolType]
public static class ChargeDefaultCardMcpTool
{
    [McpServerTool(Name = "charge_default_card")]
    [Description("Kullanicinin varsayilan kayitli kartindan GERCEK odeme ceker. YALNIZCA kullanici odemeyi acikca onayladiktan sonra cagir. amount=sepet tutari; installment=secilen taksit sayisi (tek cekim icin 1); paidPrice=o taksitin toplam odenecek tutari (get_card_installments'tan; tek cekimde amount ile ayni). Basarida paymentId + durum doner. PAN/token asla donmez.")]
    public static Task<FeatureObjectResultModel<ChargeDefaultCardForAgent.ChargeResultView>> ChargeDefaultCardAsync(
        [Description("Sepet tutari (temel fiyat), TRY")] decimal amount,
        [Description("Secilen taksit sayisi; tek cekim icin 1")] int installment,
        [Description("Secilen taksitin toplam odenecek tutari (get_card_installments'tan). Tek cekimde amount ile ayni.")] decimal paidPrice,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var user = currentUser.Load(http.HttpContext!.User);
        return bus.InvokeAsync<FeatureObjectResultModel<ChargeDefaultCardForAgent.ChargeResultView>>(
            new ChargeDefaultCardForAgent.ChargeDefaultCardCommand(
                user.Id, user.Name, user.Email, user.Phone,
                amount, paidPrice, installment <= 0 ? 1 : installment), ct);
    }
}
