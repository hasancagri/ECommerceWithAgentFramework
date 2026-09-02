namespace Catalog.Api.Domains.Products.Features.Queries;

// 058: admin tekil ürün — düzenleme formunun tek okuma kaynağı (draft dahil). Künye adlarıyla
// (yazar/yayınevi/kategori) + fiyat geçmişi (kronolojik) birlikte döner; form ikinci çağrı yapmaz.
public static class AdminGetProduct
{
    public record AdminGetProductQuery(Guid Id);

    public class AuthorItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class PriceChangeItem
    {
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class AdminGetProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string ShortDescription { get; set; } = default!;
        public string FullDescription { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string? Isbn { get; set; }
        public decimal Price { get; set; }
        public bool Published { get; set; }
        public string? ImageUrl { get; set; }
        public List<AuthorItem> Authors { get; set; } = [];
        public Guid PublisherId { get; set; }
        public string PublisherName { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<PriceChangeItem> PriceHistory { get; set; } = [];
    }

    public class AdminGetProductQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminGetProductResponse>> Handle(
            AdminGetProductQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(query.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AdminGetProductResponse>.NotFound();

            var authors = (await session.LoadManyAsync<Author>(ct, product.AuthorIds.ToArray()))
                .ToDictionary(a => a.Id, a => a.Name);
            var publisher = await session.LoadAsync<Publisher>(product.PublisherId, ct);

            // K4: dış kontrat tek kategori görür — primary = ilk atama (UpdateProduct ile aynı sözlük).
            var primaryCategoryId = product.Categories.Select(c => c.CategoryId).FirstOrDefault();
            var category = primaryCategoryId == Guid.Empty
                ? null
                : await session.LoadAsync<Category>(primaryCategoryId, ct);

            var history = await session.Query<ProductPriceChange>()
                .Where(x => x.ProductId == product.Id)
                .OrderBy(x => x.ChangedAtUtc)
                .ToListAsync(ct);

            return FeatureObjectResultModel<AdminGetProductResponse>.Ok(new AdminGetProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                Sku = product.Sku,
                Isbn = product.Gtin,
                Price = product.Price.Amount,
                Published = product.Published,
                ImageUrl = product.ImageUrl,
                Authors = product.AuthorIds
                    .Where(authors.ContainsKey)
                    .Select(id => new AuthorItem { Id = id, Name = authors[id] })
                    .ToList(),
                PublisherId = product.PublisherId,
                PublisherName = publisher?.Name ?? string.Empty,
                CategoryId = primaryCategoryId,
                CategoryName = category?.Name ?? string.Empty,
                PriceHistory = history.Select(h => new PriceChangeItem
                {
                    OldPrice = h.OldPrice,
                    NewPrice = h.NewPrice,
                    ChangedAtUtc = h.ChangedAtUtc,
                }).ToList(),
            });
        }
    }
}

public static class AdminGetProductQueryEndpoint
{
    public static RouteGroupBuilder AdminGetProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AdminGetProduct.AdminGetProductResponse>>(
                    new AdminGetProduct.AdminGetProductQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("AdminGetProduct");
        return group;
    }
}
