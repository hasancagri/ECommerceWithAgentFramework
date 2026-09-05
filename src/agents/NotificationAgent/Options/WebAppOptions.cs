namespace NotificationAgent.Options;

// Mail'deki urun linkinin MUTLAK tabani — relatif link mail istemcisinde (Mailpit UI) yanlis
// host'a cozulur (canli bulgu: 404). AppHost, WebApp endpoint referansini env ile verir.
public class WebAppOptions
{
    public const string SectionName = "WebApp";

    [Required]
    public string BaseUrl { get; set; } = default!;
}