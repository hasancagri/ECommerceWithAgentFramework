using System.Collections.Concurrent;

namespace Supplier.Api.Domains.Feeds;

// 041 mock feed satırı (contracts/mock-feed-api.md). Barkod zorunlu kimliktir; dataset her satırda taşır.
public record SupplierFeedRow(
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
    decimal Height);

// 041: tedarikçi-kodlu mock uçlar. Veri Datasets/supplier-{kod}.rev{N}.json dosyalarından istek
// anında okunur (dosya değişikliği restart'sız yansır — 005/R12 mirası). Rev başına AYRI dosya:
// advance bellek-içi rev'i artırır, endpoint o rev'in dosyasını döner; rev dosyası yoksa mevcut en
// yüksek rev'e düşer. Restart'ta rev=1'e döner (mock; kalıcılık bilinçli yok).
public static class FeedEndpointExtension
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, int> Revisions = new();

    public static void AddFeedGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("v{version:apiVersion}/feeds")
            .WithTags("Feeds")
            .WithApiVersionSet(apiVersionSet);

        // Güncel rev'e göre TAM snapshot (full feed). Bilinmeyen tedarikçi kodu → 404.
        group.MapGet("{supplierCode}", async (string supplierCode, IHostEnvironment env, CancellationToken ct) =>
            {
                var rev = Revisions.GetValueOrDefault(supplierCode, 1);
                var path = ResolveDatasetPath(env.ContentRootPath, supplierCode, rev);
                if (path is null)
                    return Results.NotFound();

                await using var stream = File.OpenRead(path);
                var rows = await JsonSerializer
                    .DeserializeAsync<List<SupplierFeedRow>>(stream, JsonOptions, ct) ?? [];
                return Results.Ok(rows);
            })
            .WithName("GetSupplierFeed");

        // Feed değişimini simüle eder: rev+1 sonraki dataset dosyasını devreye alır (US2 canlı testi).
        group.MapPost("{supplierCode}/advance", (string supplierCode, IHostEnvironment env) =>
            {
                if (ResolveDatasetPath(env.ContentRootPath, supplierCode, rev: 1) is null)
                    return Results.NotFound();

                var rev = Revisions.AddOrUpdate(supplierCode, 2, (_, current) => current + 1);
                return Results.Ok(new { supplierCode, rev });
            })
            .WithName("AdvanceSupplierFeed");
    }

    // İstenen rev'in dosyası; yoksa o tedarikçinin mevcut EN YÜKSEK rev dosyası (advance taşması
    // son bilinen feed'de sabitlenir); hiç dosya yoksa null (bilinmeyen tedarikçi → 404).
    // Kod yalnız [a-z0-9-] kabul eder — route değeri dosya yoluna girdiği için path-traversal kapısı.
    private static string? ResolveDatasetPath(string contentRoot, string supplierCode, int rev)
    {
        if (supplierCode.Length == 0 ||
            !supplierCode.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            return null;

        var datasets = Path.Combine(contentRoot, "Datasets");
        var exact = Path.Combine(datasets, $"{supplierCode}.rev{rev}.json");
        if (File.Exists(exact))
            return exact;

        return Directory.Exists(datasets)
            ? Directory.EnumerateFiles(datasets, $"{supplierCode}.rev*.json")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .FirstOrDefault()
            : null;
    }
}