namespace Order.Api.Options;

// 028: saga arka plan makine token'i (order-saga client_credentials) — section "SagaAuth".
// SagaTokenHandler buradan tip'li okur (magic-string config[...] yerine).
public class SagaAuth
{
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
}
