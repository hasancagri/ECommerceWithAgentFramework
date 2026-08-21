namespace WebApp.ViewModel;

// 016: /Products filtre seçenekleri — gerçek veriden gelir, boş kategori/marka listelenmez (US1-3).
public record FilterOptionViewModel(Guid Id, string Name);

// 043: spec facet'i — checkbox paneli; Key = query-string değeri ("Attribute|Option").
public record SpecFacetOptionViewModel(string Name, int Count, string Key);

public record SpecFacetViewModel(string Name, List<SpecFacetOptionViewModel> Options);

public record FilterOptionsViewModel(
    List<FilterOptionViewModel> Categories,
    List<FilterOptionViewModel> Brands,
    List<SpecFacetViewModel> Specifications)
{
    public static FilterOptionsViewModel Empty => new([], [], []);
}