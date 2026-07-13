namespace ProductEnrichmentAgent.Options;

// OpenAI erisim ayarlari (appsettings 'OpenAI' bolumu). ApiKey appsettings'te tutulmaz —
// user-secrets/env uzerinden gelir; Program.cs eksikse fail-fast atar.
public sealed class OpenAIOption
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public string ImageModel { get; set; } = "gpt-image-1";
}