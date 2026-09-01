namespace WebApp.Dto;

// 053: reco-trainer TasteProfile sözleşmesi (SABİT, FR-017) — WebApp'in okuduğu alanlar (camelCase JSON).
public record TasteProfileDto(
    SubjectDto Subject,
    List<ClusterDto> Clusters,
    ClusterDto? Discovery);

public record SubjectDto(Guid? UserId, Guid? AnonymousId);

public record ClusterDto(
    string Label,
    string Reason,
    double Share,
    List<AttributeDto> Attributes);

public record AttributeDto(string Type, string Value, decimal Weight);

// 053: Storefront GetRecommendedProducts istek/yanıtı (ranking). İstek = bir kuşağın öznitelik kümesi.
public record RecommendRequestDto(
    List<AttributeDto> Attributes,
    int Offset,
    int PageSize,
    List<Guid> ExcludeIds);

public record RecommendResponseDto(List<ProductCardDto> Cards);

public record ProductCardDto(
    Guid ProductId,
    string Name,
    List<CardAuthorDto> Authors,
    Guid? PublisherId,
    string? Publisher,
    decimal Price,
    decimal? RatingAverage,
    int RatingCount,
    string? ImageUrl);

public record CardAuthorDto(Guid Id, string Name);
