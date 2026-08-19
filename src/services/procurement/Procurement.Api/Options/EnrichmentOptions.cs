using System.ComponentModel.DataAnnotations;

namespace Procurement.Api.Options;

// Enrich agent model config'i — ZORUNLU, açılışta fail-fast (ValidateOnStart).
// Section adı "OpenAI" (tip-adı konvansiyonunun bilinçli istisnası): ChatAgent/eski IngestionAgent
// ile aynı user-secrets anahtarları (OpenAI:ApiKey + OpenAI:Model) kullanılır — quickstart sözleşmesi.
public class EnrichmentOptions
{
    public const string SectionName = "OpenAI";

    [Required]
    public string ApiKey { get; set; } = default!;

    [Required]
    public string Model { get; set; } = default!;
}