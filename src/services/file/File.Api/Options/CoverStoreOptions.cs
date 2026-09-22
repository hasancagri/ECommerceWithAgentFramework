using System.ComponentModel.DataAnnotations;

namespace FileApi.Options;

// Kapak deposu kök dizini (kalıcı host path). ZORUNLU — açılışta fail-fast (ValidateOnStart).
// İçerik {RootPath}/covers/{isbn} (+ {isbn}.ct sidecar). AppHost env ile enjekte eder.
public class CoverStoreOptions
{
    public const string SectionName = "CoverStore";

    [Required]
    public string RootPath { get; set; } = default!;
}
