namespace WebApp.Dto;

// 011: sayfalı vitrin yanıtı — Storefront'un paged result gövdesinden gereken alanlar.
public record StorefrontProductPagedDto(
    List<StorefrontProductDto> Data,
    int TotalItemCount,
    int PageNumber,
    int PageCount,
    bool HasPreviousPage,
    bool HasNextPage);