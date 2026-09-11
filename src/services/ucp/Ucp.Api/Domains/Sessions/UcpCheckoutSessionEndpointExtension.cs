using Microsoft.AspNetCore.Mvc;
using Ucp.Api.Domains.Sessions.Features.Commands;
using Ucp.Api.Domains.Sessions.Features.Queries;

namespace Ucp.Api.Domains.Sessions;

/// <summary>
/// UCP checkout session dış-protokol REST uçları (literal /ucp/checkout_sessions; iç sürümleme segmenti
/// yok — protokol sözleşmesi). Tümü scope dev.ucp.shopping.checkout ile korunur. Yanıt = UCP session
/// gövdesi; hata = Result + messages (404 kayıt yok, 400 doğrulama/durum).
/// </summary>
public static class UcpCheckoutSessionEndpointExtension
{
    public record LineItemBody(string ProductId, int Quantity);
    public record BuyerBody(string Email, string FirstName, string LastName);
    public record FulfillmentBody(string SelectedOptionId, string? DestinationId);
    public record CreateBody(string Currency, List<LineItemBody>? LineItems, BuyerBody? Buyer);
    public record UpdateBody(List<LineItemBody>? LineItems, BuyerBody? Buyer, FulfillmentBody? Fulfillment, List<string>? DiscountCodes);
    public record CancelBody(string? Reason);

    public static void AddUcpCheckoutSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("ucp/checkout_sessions")
            .WithTags("UcpCheckout")
            .RequireAuthorization(AuthorizationScopes.UcpCheckout);

        // create
        group.MapPost("", async ([FromBody] CreateBody body, IMessageBus bus) =>
        {
            var cmd = new CreateSession.CreateSessionCommand(
                body.Currency,
                (body.LineItems ?? []).Select(l => new CreateSession.LineItemInput(l.ProductId, l.Quantity)).ToList(),
                body.Buyer is null ? null : new CreateSession.BuyerInput(body.Buyer.Email, body.Buyer.FirstName, body.Buyer.LastName));
            var result = await bus.InvokeAsync<FeatureObjectResultModel<SessionResult>>(cmd);
            return ToResponse(result);
        });

        // update (tam değişim)
        group.MapPost("{id}", async (string id, [FromBody] UpdateBody body, IMessageBus bus) =>
        {
            var cmd = new UpdateSession.UpdateSessionCommand(
                id,
                body.LineItems?.Select(l => new UpdateSession.LineItemInput(l.ProductId, l.Quantity)).ToList(),
                body.Buyer is null ? null : new UpdateSession.BuyerInput(body.Buyer.Email, body.Buyer.FirstName, body.Buyer.LastName),
                body.Fulfillment is null ? null : new UpdateSession.FulfillmentInput(body.Fulfillment.SelectedOptionId, body.Fulfillment.DestinationId),
                body.DiscountCodes);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<SessionResult>>(cmd);
            return ToResponse(result);
        });

        // get
        group.MapGet("{id}", async (string id, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<SessionResult>>(new GetSession.GetSessionQuery(id));
            return ToResponse(result);
        });

        // complete (Idempotency-Key zorunlu)
        group.MapPost("{id}/complete", async (string id, HttpRequest request, IMessageBus bus) =>
        {
            var key = request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key))
                return Results.BadRequest(FeatureObjectResultModel<SessionResult>.Error(
                    new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED }));

            var result = await bus.InvokeAsync<FeatureObjectResultModel<SessionResult>>(
                new CompleteSession.CompleteSessionCommand(id, key));
            return ToResponse(result);
        });

        // cancel
        group.MapPost("{id}/cancel", async (string id, [FromBody] CancelBody? body, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<SessionResult>>(
                new CancelSession.CancelSessionCommand(id, body?.Reason));
            return ToResponse(result);
        });
    }

    // UCP yanıtı = session gövdesi (Data); hata = Result + messages. RECORD_NOT_FOUND → 404, aksi 400.
    private static IResult ToResponse(FeatureObjectResultModel<SessionResult> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Data);

        var notFound = result.Messages?.Any(m => m.Code == UcpResourceConstants.RECORD_NOT_FOUND) == true;
        return notFound ? Results.NotFound(result) : Results.BadRequest(result);
    }
}