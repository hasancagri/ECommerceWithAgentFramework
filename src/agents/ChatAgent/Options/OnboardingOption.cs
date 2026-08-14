namespace ChatAgent.Options;

// 032/029: admin onboarding persona'sina boot'ta verilecek sabitler — section "Onboarding".
// 016 push-inline: submit_registration basvuru alanlarini dogrudan alir; bu magazanin sabit kimlik
// alanlari prompt'a gomulur. 029 alan seti gateway'in 023 Merchant sozlesmesiyle birebir:
// Email = registration_status sorgu anahtari; tipe gore kosullu alanlar (TCKN/vergi) opsiyonel.
public class OnboardingOption
{
    public string Type { get; set; } = "LimitedOrJointStockCompany";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string GsmNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string Iban { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactSurname { get; set; } = "";
    public string IdentityNumber { get; set; } = "";
    public string TaxOffice { get; set; } = "";
    public string TaxNumber { get; set; } = "";
    public string LegalCompanyTitle { get; set; } = "";
}
