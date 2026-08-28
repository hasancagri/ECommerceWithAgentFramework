using System.Text.Json;
using Catalog.Api.Domains.Products.Features.Commands;

namespace Catalog.Api.Seeding;

// 051: kitap toplu import seeder'ı. Açılışta İş1 çıktısı books.json'u okur ve her kitap için
// ImportBook.Command'ı IMessageBus ile çağırır (domain mantığı + event yayımı handler'da; seeder ince).
// İdempotent: ImportBook deterministik ProductId ile upsert eder → re-run çoğaltmaz.
public sealed class BookImportHostedService(
    IServiceProvider services,
    ILogger<BookImportHostedService> logger) : IHostedService
{
    private sealed record BookRecord(
        string Isbn, string Title, string Brand, decimal? PriceTry,
        string? ImageUrl, string CategoryMid, string CategoryLeaf);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Seeding", "Data", "books.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("Kitap import atlandı: {Path} bulunamadı", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var books = await JsonSerializer.DeserializeAsync<List<BookRecord>>(stream, JsonOptions, cancellationToken)
                    ?? [];

        // Command çağrıları IMessageBus üzerinden (her biri kendi [Transactional] kapsamında) — scope aç.
        using var scope = services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var published = 0;
        var draft = 0;
        foreach (var b in books)
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<ImportBook.ImportBookResponse>>(
                new ImportBook.ImportBookCommand(
                    b.Isbn, b.Title, b.Brand, b.PriceTry, b.ImageUrl, b.CategoryMid, b.CategoryLeaf),
                cancellationToken);

            if (result.IsSuccess && result.Data!.Published) published++;
            else draft++;
        }

        logger.LogInformation("Kitap import tamam: {Total} kitap ({Published} yayında, {Draft} taslak/fiyatsız)",
            books.Count, published, draft);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}