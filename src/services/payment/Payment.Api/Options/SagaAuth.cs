namespace Payment.Api.Options;

// 077: Payment.Api arka plan makine token'ı (payment-s2s client_credentials) — section "SagaAuth".
// PaymentTokenHandler buradan tip'li okur (Customer merchant-key S2S çağrısı; customer.read scope).
public class SagaAuth
{
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
}