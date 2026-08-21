namespace WebApp.Dto;

// 016: storefront facet ucu — satılabilir satırlardan türetilen kimlik+ad seçenekleri.
public record FilterOptionDto(Guid Id, string Name);

// 043: spec facet'leri — ad + option(ad,count).
public record SpecFacetOptionDto(string Name, int Count);

public record SpecFacetDto(string Name, List<SpecFacetOptionDto> Options);

public record StorefrontFilterOptionsDto(
    List<FilterOptionDto> Categories,
    List<FilterOptionDto> Brands,
    List<SpecFacetDto>? Specifications);