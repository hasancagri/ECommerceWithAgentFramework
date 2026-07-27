namespace WebApp.ViewModel;

// 011: bir sayfalık ürün kümesi + pager meta'sı (Tüm Ürünler ve ana sayfa bunu tüketir).
public record PagedProductListViewModel(
    List<StorefrontProductViewModel> Products,
    int PageNumber,
    int PageCount,
    int TotalItemCount)
{
    // FR-006: boş vitrin / aralık dışı sayfa hatasız boş duruma çevrilir.
    public static PagedProductListViewModel Empty(int pageNumber) => new([], pageNumber, 0, 0);
}

// 011 FR-003/004: numaralı pager; tek sayfada hiç çizilmez (partial içinde kontrol edilir).
// 016: FilterQuery — sayfa linklerine eklenen hazır query eki ("&categoryId=...") ; filtre sayfalamada korunur.
public record PagerViewModel(int PageNumber, int PageCount, string? FilterQuery = null)
{
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < PageCount;
}