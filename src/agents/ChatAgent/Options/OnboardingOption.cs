namespace ChatAgent.Options;

// 032: admin onboarding persona'sina boot'ta verilecek sabitler — section "Onboarding".
// DescriptorUrl bos ise Program.cs WebApp service-discovery adresinden turetir. Domain = bu magazanin
// gateway'e kaydedilecek alan adi (registration_status sorgusu bununla yapilir). House-style Options.
public class OnboardingOption
{
    // Bos birakilirsa WebApp'in well-known descriptor'undan turetilir (Aspire service discovery).
    public string? DescriptorUrl { get; set; }

    public string Domain { get; set; } = "shop.dropshop.local";
}