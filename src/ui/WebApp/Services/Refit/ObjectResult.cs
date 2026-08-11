namespace WebApp.Services.Refit;

// Customer API tekil-nesne uclarinin FeatureObjectResultModel zarfi (yalniz ihtiyac duyulan alanlar).
public class ObjectResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
}
