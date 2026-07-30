namespace WebApp.Services.Refit;

// Customer API list uclarinin FeatureListResultModel zarfi (yalniz ihtiyac duyulan alanlar).
public class ListResult<T>
{
    public bool IsSuccess { get; set; }
    public List<T>? Data { get; set; }
}