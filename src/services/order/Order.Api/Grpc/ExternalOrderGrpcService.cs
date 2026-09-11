using Shared.Grpc.ExternalOrder;
using Order.Api.Domains.Orders.Features.Agents;

namespace Order.Api.Grpc;

/// <summary>
/// 072: UCP → Order sanksiyonlu gRPC sunucusu — İNCE sarmalayıcı (iş mantığı yok, IMessageBus'a devreder;
/// İlke I). Yetki endpoint'te (order.write; Program MapGrpcService.RequireAuthorization). global::Grpc.Core
/// çakışma dersi (Order.A2A). Charge + already-captured devir CreateExternalOrder slice'ında.
/// </summary>
public class ExternalOrderGrpcService(IMessageBus bus) : ExternalOrder.ExternalOrderBase
{
    public override async Task<CreateExternalOrderReply> CreateExternalOrder(
        CreateExternalOrderRequest request, global::Grpc.Core.ServerCallContext context)
    {
        var command = new CreateExternalOrder.CreateExternalOrderCommand(
            ExternalRef: request.ExternalRef,
            Currency: request.Currency,
            AmountMinor: request.AmountMinor,
            Buyer: new CreateExternalOrder.BuyerInfo(
                request.Buyer?.Email ?? "", request.Buyer?.FirstName ?? "", request.Buyer?.LastName ?? ""),
            Items: request.LineItems
                .Select(i => new CreateExternalOrder.ItemInfo(i.ProductId, i.Quantity, i.UnitPriceMinor))
                .ToList(),
            UcpPaymentRef: request.UcpPaymentRef);

        var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateExternalOrder.CreateExternalOrderResponse>>(
            command, context.CancellationToken);

        var data = result.Data;
        return new CreateExternalOrderReply
        {
            OrderRef = data?.OrderRef ?? "",
            Charged = data?.Charged ?? false,
            Message = data?.Message ?? "Sipariş oluşturulamadı."
        };
    }
}