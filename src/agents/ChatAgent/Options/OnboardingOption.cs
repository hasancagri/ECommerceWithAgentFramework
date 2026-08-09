namespace ChatAgent.Options;

// 032: admin onboarding persona'sina boot'ta verilecek sabitler — section "Onboarding".
// 016 push-inline: submit_registration basvuru alanlarini dogrudan alir (descriptor URL YOK); bu magazanin
// sabit kimlik alanlari prompt'a gomulur. Domain = registration_status sorgu anahtari. House-style Options.
public class OnboardingOption
{
    public string Domain { get; set; } = "shop.dropshop.local";
    public string LegalName { get; set; } = "";
    public string TaxId { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
}