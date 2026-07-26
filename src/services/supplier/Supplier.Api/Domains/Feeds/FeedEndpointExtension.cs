using System.Text.Json;

namespace Supplier.Api.Domains.Feeds;

// Tek tedarikçi feed ucu: anonim, full snapshot. Datasets/products.json istek anında okunur,
// SupplierProduct listesine çözülür ve tipli olarak döner; dosya değişikliği restart'sız yansır.
public static class FeedEndpointExtension
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void AddFeedGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("v{version:apiVersion}/feeds")
            .WithTags("Feeds")
            .WithApiVersionSet(apiVersionSet);

        group.MapGet("", async (IHostEnvironment env, CancellationToken ct) =>
            {
                var path = Path.Combine(env.ContentRootPath, "Datasets", "products.json");
                await using var stream = File.OpenRead(path);
                var products = await JsonSerializer
                    .DeserializeAsync<List<SupplierProduct>>(stream, JsonOptions, ct) ?? [];
                return Results.Ok(products);
            })
            .WithName("GetSupplierFeed");
    }
}

// Dataset dosyasındaki kanonik kayıt (DTO — Marten dokümanı DEĞİL, simülatör DB'siz; R12).
// Feed ucu products.json'u bu tipe çözüp olduğu gibi döner; IngestionAgent aynı şemayı okur.
public record SupplierProduct(
    string ExternalId,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    int StockQuantity,
    string? DiscountCode,
    decimal? DiscountPercent);