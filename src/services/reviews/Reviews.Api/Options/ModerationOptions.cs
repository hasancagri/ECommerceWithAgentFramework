using System.ComponentModel.DataAnnotations;

namespace Reviews.Api.Options;

// Moderasyon agent model config'i — ZORUNLU, acilista fail-fast (ValidateOnStart).
// Section adi "OpenAI" (tip-adi konvansiyonunun bilincli istisnasi): Procurement EnrichmentOptions
// emsali — ayni user-secrets anahtarlari (OpenAI:ApiKey + OpenAI:Model) kullanilir.
public class ModerationOptions
{
    public const string SectionName = "OpenAI";

    [Required]
    public string ApiKey { get; set; } = default!;

    [Required]
    public string Model { get; set; } = default!;
}
