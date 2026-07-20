using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Discount.Api.Domains.Discounts;

[McpServerToolType]
public static class GetDiscountByProductIdMcpTool
{
    [McpServerTool(Name = "get_discount_by_product")]
    [Description("Bir ürüne tanımlı indirim oranını (varsa) ürün Id'sine göre getirir.")]
    public static Task<FeatureObjectResultModel<GetDiscountByProductId.GetDiscountByProductIdResponse>> GetDiscountByProductIdAsync(
        [Description("İndirim durumu sorgulanacak ürünün Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetDiscountByProductId.GetDiscountByProductIdResponse>>(
            new GetDiscountByProductId.GetDiscountByProductIdQuery(productId), ct);
}