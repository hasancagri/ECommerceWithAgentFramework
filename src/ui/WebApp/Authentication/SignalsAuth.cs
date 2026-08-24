namespace WebApp.Authentication;

// 048: WebApp davranış-sinyali m2m istemcisi (webapp-signals client_credentials). Config section
// "SignalsAuth". PersonalizationSignalsTokenHandler buradan tip'li okur (magic-string yerine).
public class SignalsAuth
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}