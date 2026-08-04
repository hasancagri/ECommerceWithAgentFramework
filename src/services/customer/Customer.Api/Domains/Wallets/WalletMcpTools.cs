using System.ComponentModel;
using ModelContextProtocol.Server;

using Agent = Customer.Api.Domains.Wallets.Features.Agent;

namespace Customer.Api.Domains.Wallets;

// MCP okuma-yalniz: KART EKLEME ASLA bir tool DEGIL (ham PAN LLM turuna girmez, FR-019).
// Sil/varsayilan-yap da tool degil — yalniz REST/WebApp. Burada yalniz guvenli listeleme.
[McpServerToolType]
public static class ListCardsMcpTool
{
    [McpServerTool(Name = "list_cards")]
    [Description("Giris yapmis kullanicinin kayitli kartlarini listeler (yalniz marka + son 4 hane + son-kullanma + etiket; PAN/CVV/token asla).")]
    public static Task<FeatureListResultModel<Agent.GetCards.CardView>> ListCardsAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<Agent.GetCards.CardView>>(
            new Agent.GetCards.GetCardsQuery(userId), ct);
    }
}

// 024: taksit sorgusu icin default kartin BIN'ini (ilk 6 hane) veren okuma tool'u. HASSAS DEGIL.
// PAN/CVV/token ASLA. Default kart yoksa NotFound (assistant BIN'siz genel sorgu / kart ekle ister).
[McpServerToolType]
public static class DefaultCardBinMcpTool
{
    [McpServerTool(Name = "get_default_card_bin")]
    [Description("Kullanicinin varsayilan kartinin BIN'ini (ilk 6 hane, banka tespiti icin) + marka + son 4 hane doner. Taksit sorgusunda kullanilir. PAN/CVV/token asla donmez. Varsayilan kart yoksa bulunamaz.")]
    public static Task<FeatureObjectResultModel<Agent.GetDefaultCardBin.DefaultCardBinView>> GetDefaultCardBinAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Agent.GetDefaultCardBin.DefaultCardBinView>>(
            new Agent.GetDefaultCardBin.GetDefaultCardBinQuery(userId), ct);
    }
}