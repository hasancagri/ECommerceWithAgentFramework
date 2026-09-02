using System.ComponentModel.DataAnnotations;

namespace Mail.Mcp.Options;

// SMTP hedefi (dev: Mailpit) — Host/Port AppHost'tan Mailpit endpoint referansıyla env olarak gelir
// (Smtp__Host/Smtp__Port); From sabit config. ZORUNLU — açılışta fail-fast (ValidateOnStart).
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = default!;

    [Required]
    public int Port { get; set; }

    [Required]
    public string From { get; set; } = default!;
}