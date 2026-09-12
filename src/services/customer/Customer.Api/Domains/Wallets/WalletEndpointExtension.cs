namespace Customer.Api.Domains.Wallets;

// Müşteri kart REST yüzeyi tamamen söküldü (MCP-only); geriye yalnız Order.Api'nin
// okuduğu yapısal S2S ucu kaldı.
public static class WalletEndpointExtension
{
    // 039: Order.Api chat siparis tamamlamada odeme baglamini (buyer + vaultToken + varsayilan adres +
    // merchantId) YAPISAL kanaldan ceker. get_payment_context bir MCP tool'u (yalniz agent yuzeyi); Order.Api
    // agent DEGIL -> imperatif MCP cagiramaz (Ilke I). Bu yapisal ikiz ayni GetPaymentContextForAgent
    // query'sini IMessageBus ile cagirir. Auth: customer.read (Order makine token'i). Buyer profil alanlari
    // (ad/email/telefon) gecilmez -> 033/038 sandbox varsayilanlarina duser (contract'ta dokumante).
    public static void AddPaymentContextInternalEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/internal/payment-context")
            .WithTags("PaymentContextInternal")
            .WithApiVersionSet(apiVersionSet)
            .MapGet("/", async (Guid userId, string? cardHandle, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetPaymentContextForAgent.PaymentContextView>>(
                    new GetPaymentContextForAgent.GetPaymentContextQuery(
                        userId, CustomerName: null, CustomerEmail: null, CustomerPhone: null, cardHandle), ct);

                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
    }

    // 075 US1: hosted kart-ekleme callback ucu. PG (kullanıcı formu bitirince) buraya POST eder;
    // conversationId = AddCardSession.Id → CompleteAddCard tetiklenir (UserId session'dan çözülür).
    // JWT'siz (dış makine çağrısı token taşımaz — R3); güvenlik = tahmin-edilemez tek-kullanımlık
    // session + PG doğrulaması. Anonim (RequireAuthorization YOK).
    public static void AddCardCallbackEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/wallet-callback")
            .WithTags("WalletCardCallback")
            .WithApiVersionSet(apiVersionSet)
            .MapPost("/card", async (CardCallbackRequest req, IMessageBus bus, CancellationToken ct) =>
            {
                if (req.ConversationId == Guid.Empty)
                    return Results.BadRequest();

                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new CompleteAddCardForAgent.CompleteAddCardCommand(req.ConversationId), ct);

                return result.IsSuccess ? Results.Ok() : Results.BadRequest(result);
            });
    }

    // PG push gövdesi (contract: {conversationId, status, pgUserHandle}); mağaza sonucu PG'den yeniden
    // pull ettiği için yalnız conversationId gerekli (status/handle yok sayılır — tek gerçek kaynak PG).
    public record CardCallbackRequest(Guid ConversationId);
}