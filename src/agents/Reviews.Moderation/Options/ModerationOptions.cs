using System.ComponentModel.DataAnnotations;

namespace Reviews.Moderation.Options;

// Moderasyon agent model config'i — ZORUNLU, acilista fail-fast (ValidateOnStart).
// Section adi "OpenAI" (tip-adi konvansiyonunun bilincli istisnasi): ayni user-secrets anahtarlari
// (OpenAI:ApiKey + OpenAI:Model). 046: Reviews.Api'den bu worker'a tasindi.
public class ModerationOptions
{
    public const string SectionName = "OpenAI";

    [Required]
    public string ApiKey { get; set; } = default!;

    [Required]
    public string Model { get; set; } = default!;
}
