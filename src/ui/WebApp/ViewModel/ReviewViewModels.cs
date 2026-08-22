namespace WebApp.ViewModel;

// 044: detay sayfasi yorum listesi (maskeli ad + rozet UI'da).
public record ReviewItemViewModel(string MaskedName, int Rating, string? Text, DateTime CreatedTime);

public record ReviewListViewModel(
    List<ReviewItemViewModel> Reviews,
    int PageNumber,
    int PageCount,
    int TotalItemCount)
{
    public static ReviewListViewModel Empty { get; } = new([], 1, 0, 0);
}
