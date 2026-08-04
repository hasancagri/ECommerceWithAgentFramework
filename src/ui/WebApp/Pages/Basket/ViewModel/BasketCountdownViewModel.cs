namespace WebApp.Pages.Basket.ViewModel;

// 025: header geri sayimi icin hafif model. IsActive false ise header hicbir sey gostermez.
// ExpiresAtIso = sunucu mutlak bitis ani (ISO-8601 UTC); istemci bunu MM:SS'e cevirir.
public record BasketCountdownViewModel(bool IsActive, string? ExpiresAtIso)
{
    public static BasketCountdownViewModel Inactive() => new(false, null);
}