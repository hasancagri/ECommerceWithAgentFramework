namespace WebApp.ViewModel;

// 053: ana sayfa çoklu-kuşak feed. Her kuşak = bir ilgi kümesi (başlık + gerekçe + kartlar).
public record HomeShelfViewModel(
    string Title,
    string Reason,
    List<StorefrontProductViewModel> Products);

// IsColdStart = profil yok/boş → popüler vitrin fallback (FR-011). Feed hiç boş/kırık render edilmez.
public record HomeFeedViewModel(
    List<HomeShelfViewModel> Shelves,
    bool IsColdStart);
