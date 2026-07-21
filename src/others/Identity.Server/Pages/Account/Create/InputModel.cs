using System.ComponentModel.DataAnnotations;

namespace Identity.Server.Pages.Create;

public class InputModel
{
    [Required]
    public string? Username { get; set; }

    [Required]
    public string? Password { get; set; }

    public string? Name { get; set; }
    public string? Email { get; set; }

    // Kullanicinin kayitta sectigi yetkiler (yalnizca operator-sunulan kumeden; FR-013).
    // Kaydedilirken UserScopes'a yazilir; UserKey bunlari miras alir.
    public List<string> SelectedScopes { get; set; } = [];

    public string? ReturnUrl { get; set; }

    public string? Button { get; set; }
}
