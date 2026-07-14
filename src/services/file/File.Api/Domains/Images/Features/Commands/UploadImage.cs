namespace File.Api.Domains.Images.Features.Commands;

// Gorsel byte'larini ProductId'ye gore idempotent alir, 256x256'ya kucultup
// Images/{ProductId}.png'e yazar ve deterministik public URL doner (DB YOK).
public static class UploadImage
{
    // Servis edilen gorsel boyutu: kaynak buyuk gelebilir, File.Api 256x256'ya kucultur.
    private const int Size = 256;

    [RequiredScope(AuthorizationScopes.FileWrite)]
    public record UploadImageCommand(Guid ProductId, string ContentBase64, string ContentType);

    public class UploadImageResponse
    {
        // Gateway uzerinden servis edilen public URL (/file → file-api, /images statik).
        public string Url { get; set; } = null!;
    }

    public class UploadImageCommandHandler
    {
        public async Task<FeatureObjectResultModel<UploadImageResponse>> Handle(
            UploadImageCommand cmd,
            IHostEnvironment env,
            CancellationToken ct)
        {
            var url = $"/file/images/{cmd.ProductId}.png";
            var dir = Path.Combine(env.ContentRootPath, "Images");
            var path = Path.Combine(dir, $"{cmd.ProductId}.png");

            // Idempotency (FR-010): asset zaten varsa yeniden uretmez; mevcut URL'i doner.
            if (System.IO.File.Exists(path))
                return FeatureObjectResultModel<UploadImageResponse>.Ok(new UploadImageResponse { Url = url });

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(cmd.ContentBase64);
            }
            catch (FormatException)
            {
                return FeatureObjectResultModel<UploadImageResponse>.Error(new MessageItem
                {
                    Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT
                });
            }

            Directory.CreateDirectory(dir);

            using (var image = Image.Load(bytes))
            {
                image.Mutate(x => x.Resize(Size, Size));
                await image.SaveAsPngAsync(path, ct);
            }

            return FeatureObjectResultModel<UploadImageResponse>.Ok(new UploadImageResponse { Url = url });
        }
    }
}