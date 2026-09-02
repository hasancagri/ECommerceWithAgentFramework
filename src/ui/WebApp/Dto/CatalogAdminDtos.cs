namespace WebApp.Dto;

// 058: Catalog yönetim penceresi DTO'ları (admin ürün liste + düzenleme formu).

public record AdminProductListItemDto(
    Guid Id,
    string Name,
    string? Isbn,
    decimal Price,
    bool Published,
    string? ImageUrl,
    List<string> AuthorNames);

// FeaturePagedResultModel zarfı (Storefront paged deseniyle aynı).
public record AdminProductPagedDto(
    List<AdminProductListItemDto> Data,
    int TotalItemCount,
    int PageNumber,
    int PageCount,
    bool HasPreviousPage,
    bool HasNextPage);

public record AdminAuthorDto(Guid Id, string Name);

public record AdminPriceChangeDto(decimal? OldPrice, decimal NewPrice, DateTime ChangedAtUtc);

public record AdminProductDetailDto(
    Guid Id,
    string Name,
    string ShortDescription,
    string FullDescription,
    string Sku,
    string? Isbn,
    decimal Price,
    bool Published,
    string? ImageUrl,
    List<AdminAuthorDto> Authors,
    Guid PublisherId,
    string PublisherName,
    Guid CategoryId,
    string CategoryName,
    List<AdminPriceChangeDto> PriceHistory);

public record CatalogLookupDto(Guid Id, string Name);

public record CategoryLookupDto(Guid Id, string Name, Guid? ParentCategoryId, int DisplayOrder, bool Published);

public record UpdateProductRequestDto(
    Guid Id,
    string Name,
    string ShortDescription,
    string FullDescription,
    decimal Price,
    string Sku,
    List<Guid> AuthorIds,
    List<string>? NewAuthorNames,
    Guid? PublisherId,
    string? NewPublisherName,
    Guid CategoryId,
    string? ImageUrl);

public record SetProductPublishedRequestDto(Guid Id, bool Published);

public record SetStockQuantityRequestDto(Guid ProductId, int Quantity);