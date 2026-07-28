namespace WebApp.Dto;

// 016: storefront facet ucu — satılabilir satırlardan türetilen kimlik+ad seçenekleri.
public record FilterOptionDto(Guid Id, string Name);

public record StorefrontFilterOptionsDto(
    List<FilterOptionDto> Categories,
    List<FilterOptionDto> Brands);