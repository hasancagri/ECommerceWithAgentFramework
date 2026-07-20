namespace Storefront.Api.Domains.StorefrontView;

[McpServerToolType]
public static class GetProductStorefrontViewMcpTool
{
    [McpServerTool(Name = "get_product_storefront_view")]
    [Description("Bir ürünün Catalog+Stock+Discount birleşik vitrin görünümünü (ad, görsel, stok durumu, indirim oranı) döner.")]
    public static Task<FeatureObjectResultModel<GetProductStorefrontView.ProductStorefrontViewResponse>> GetProductStorefrontViewAsync(
        [Description("Vitrin görünümü sorgulanacak ürünün Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetProductStorefrontView.ProductStorefrontViewResponse>>(
            new GetProductStorefrontView.GetProductStorefrontViewQuery(productId), ct);
}