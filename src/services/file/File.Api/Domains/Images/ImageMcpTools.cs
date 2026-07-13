using File.Api.Domains.Images.Features.Commands;

namespace File.Api.Domains.Images;

[McpServerToolType]
public static class UploadProductImageMcpTool
{
    [McpServerTool(Name = "upload_product_image")]
    [Description("Urun gorselini ProductId'ye gore idempotent yukler (256x256 PNG) ve servis edilen public URL'i doner.")]
    public static Task<FeatureObjectResultModel<UploadImage.UploadImageResponse>> UploadProductImageAsync(
        [Description("Gorselin ait oldugu urun Id'si")] Guid productId,
        [Description("PNG gorselin base64 icerigi")] string contentBase64,
        [Description("Icerik tipi, ornegin image/png")] string contentType,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<UploadImage.UploadImageResponse>>(
            new UploadImage.UploadImageCommand(productId, contentBase64, contentType), ct);
}