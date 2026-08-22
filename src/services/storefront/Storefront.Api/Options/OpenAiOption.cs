namespace Storefront.Api.Options;

// 019: OpenAI embedding config'i (appsettings "OpenAI"). Fail-fast: ApiKey/EmbeddingModel zorunlu —
// eksikse ValidateOnStart servis acilisini durdurur (config[...] magic-string yerine tip'li POCO).
public class OpenAiOption
{
    [System.ComponentModel.DataAnnotations.Required]
    public string ApiKey { get; set; } = default!;

    [System.ComponentModel.DataAnnotations.Required]
    public string EmbeddingModel { get; set; } = default!;
}
