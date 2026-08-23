namespace Supplier.Api.Domains.Feeds;

// 047 mock feed — HETEROJEN: her tedarikçi kendi route'unu + kendi FARKLI JSON şeklini döndürür
// (gerçek dropship emsali). Veri Datasets/{supplierCode}.json'dan istek anında okunur (dosya değişikliği
// restart'sız yansır — feed değişimi dosyayı ELLE düzenleyerek simüle edilir; advance/rev SÖKÜLDÜ).

// Tedarikçi A şekli — "yerli" sözlük (barcode/name/price/stock).
public record SupplierAFeedRow(
    string Barcode,
    string SupplierSku,
    string Name,
    string? Description,
    string Brand,
    string? Category,
    decimal Price,
    int Stock,
    decimal Weight,
    decimal Length,
    decimal Width,
    decimal Height,
    Dictionary<string, string>? Attributes = null,
    string? FamilyCode = null);

// Tedarikçi B şekli — FARKLI sözlük (gtin/title/cost/warehouseQty). Procurement ACL adapter'ı normalize eder.
public record SupplierBFeedRow(
    string Gtin,
    string Sku,
    string Title,
    string? Details,
    string Manufacturer,
    string? CategoryPath,
    decimal Cost,
    int WarehouseQty,
    SupplierBDimensions? DimensionsCm,
    Dictionary<string, string>? Specs = null,
    string? VariantGroup = null);

public record SupplierBDimensions(decimal W, decimal L, decimal Wd, decimal H);

public static class FeedEndpointExtension
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void AddFeedGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("v{version:apiVersion}/feeds")
            .WithTags("Feeds")
            .WithApiVersionSet(apiVersionSet);

        // Her tedarikçi AYRI route + AYRI şekil (heterojen). Dosya istek anında okunur.
        group.MapGet("supplier-a", (IHostEnvironment env, CancellationToken ct)
                => ReadDatasetAsync<SupplierAFeedRow>(env.ContentRootPath, "supplier-a", ct))
            .WithName("GetSupplierAFeed");

        group.MapGet("supplier-b", (IHostEnvironment env, CancellationToken ct)
                => ReadDatasetAsync<SupplierBFeedRow>(env.ContentRootPath, "supplier-b", ct))
            .WithName("GetSupplierBFeed");
    }

    // Datasets/{code}.json → tipli liste. Dosya yoksa 404 (bilinmeyen tedarikçi).
    private static async Task<IResult> ReadDatasetAsync<TRow>(
        string contentRoot, string supplierCode, CancellationToken ct)
    {
        var path = Path.Combine(contentRoot, "Datasets", $"{supplierCode}.json");
        if (!File.Exists(path))
            return Results.NotFound();

        await using var stream = File.OpenRead(path);
        var rows = await JsonSerializer.DeserializeAsync<List<TRow>>(stream, JsonOptions, ct) ?? [];
        return Results.Ok(rows);
    }
}
