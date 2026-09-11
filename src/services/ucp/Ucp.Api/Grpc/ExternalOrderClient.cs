using Shared.Grpc.ExternalOrder;

namespace Ucp.Api.Grpc;

/// <summary>
/// UCP → Order sanksiyonlu gRPC istemci sarmalayıcısı. Session complete anında already-captured sipariş
/// devrini çağırır; charge Order İÇİNDE yapılır (UCP BC PG'ye dokunmaz — İlke I). external_ref =
/// SessionId (deterministik OrderId → idempotent). Erişilemezse fail-closed (charged=false + mesaj);
/// gRPC namespace çakışması için global::Grpc.Core (Order.A2A dersi).
/// </summary>
public sealed class ExternalOrderClient(
    ExternalOrder.ExternalOrderClient client,
    UcpPlatformOption platform,
    ILogger<ExternalOrderClient> logger) : IScopedDependency
{
    public record Outcome(string OrderRef, bool Charged, string Message);

    public async Task<Outcome> CreateAsync(UcpCheckoutSession session, CancellationToken ct)
    {
        var request = new CreateExternalOrderRequest
        {
            ExternalRef = session.SessionId,
            Currency = session.Currency,
            AmountMinor = session.Totals.GrandTotalMinor,
            UcpPaymentRef = platform.PaymentInstrument,
            Buyer = new Buyer
            {
                Email = session.Buyer?.Email ?? "",
                FirstName = session.Buyer?.FirstName ?? "",
                LastName = session.Buyer?.LastName ?? ""
            }
        };
        foreach (var item in session.LineItems)
            request.LineItems.Add(new LineItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPriceMinor = item.UnitPriceMinor
            });

        try
        {
            var reply = await client.CreateExternalOrderAsync(request, cancellationToken: ct);
            return new Outcome(reply.OrderRef, reply.Charged, reply.Message);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            // Order erişilemez / iç hata → sipariş oluşmadı say (fail-closed); complete tamamlanmaz.
            logger.LogWarning(ex, "UCP → Order CreateExternalOrder gRPC hatası (session {SessionId}).", session.SessionId);
            return new Outcome("", false, UcpResourceConstants.ORDER_HANDOFF_UNAVAILABLE);
        }
    }
}