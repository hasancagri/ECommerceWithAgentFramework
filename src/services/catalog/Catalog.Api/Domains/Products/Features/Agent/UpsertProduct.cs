namespace Catalog.Api.Domains.Products.Features.Agent;

// Agent'a açık TEK yazma yüzü (005): SKU-anahtarlı upsert. Create/update kararı LLM'de değil,
// burada deterministik koddadır — SKU varsa güncelle, yoksa oluştur. Retry doğal yakınsar:
// zarf kaybolsa bile ikinci deneme aynı SKU'yu bulur, kopya ürün oluşamaz.
// İş mantığı kopyalanmaz; mevcut Create/Update command'larına delege edilir (yazma yolu tek).
// 016: marka/kategori artık ad değil Id alır — Id'ler zincirin Brand/CategoryWrite adımlarından gelir (R10).
// İkisi de zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün yazılamaz.
public static class UpsertProduct
{
    public record UpsertProductCommand(
        string Name,
        string Description,
        decimal Price,
        string Sku,
        Guid BrandId,
        Guid CategoryId,
        string? ImageUrl);

    public class UpsertProductResponse
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = default!; // "created" | "updated"
    }

    public class UpsertProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpsertProductResponse>> Handle(
            UpsertProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var existing = await session.Query<Product>()
                .FirstOrDefaultAsync(x => x.Sku == cmd.Sku && !x.IsDeleted, ct);

            if (existing is null)
            {
                var created = await bus.InvokeAsync<FeatureObjectResultModel<Commands.CreateProduct.CreateProductResponse>>(
                    new Commands.CreateProduct.CreateProductCommand(
                        cmd.Name, cmd.Description, cmd.Price, cmd.Sku, cmd.BrandId, cmd.CategoryId, cmd.ImageUrl), ct);

                if (!created.IsSuccess)
                    return FeatureObjectResultModel<UpsertProductResponse>.Error(created.Messages);

                return FeatureObjectResultModel<UpsertProductResponse>.Ok(new UpsertProductResponse
                {
                    Id = created.Data!.Id,
                    Action = "created"
                });
            }

            var updated = await bus.InvokeAsync<FeatureObjectResultModel<Commands.UpdateProduct.UpdateProductResponse>>(
                new Commands.UpdateProduct.UpdateProductCommand(
                    existing.Id, cmd.Name, cmd.Description, cmd.Price, cmd.Sku, cmd.BrandId, cmd.CategoryId, cmd.ImageUrl), ct);

            if (!updated.IsSuccess)
                return FeatureObjectResultModel<UpsertProductResponse>.Error(updated.Messages);

            return FeatureObjectResultModel<UpsertProductResponse>.Ok(new UpsertProductResponse
            {
                Id = updated.Data!.Id,
                Action = "updated"
            });
        }
    }
}