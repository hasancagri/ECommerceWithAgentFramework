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