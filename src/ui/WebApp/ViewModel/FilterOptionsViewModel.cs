namespace WebApp.ViewModel;

// 016: /Products filtre seçenekleri — gerçek veriden gelir, boş kategori/marka listelenmez (US1-3).
public record FilterOptionViewModel(Guid Id, string Name);

public record FilterOptionsViewModel(
    List<FilterOptionViewModel> Categories,
    List<FilterOptionViewModel> Brands)
{
    public static FilterOptionsViewModel Empty => new([], []);
}