using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 075: Payment BC S2S makine token'i (client_credentials) — section "SagaAuth". PG NON-3D çekimi için
// Customer payment-context + merchant-key S2S uçlarını çağırırken kullanılır (customer.read scope).
// order-saga istemcisi paylaşılır (superset scope; magic-string config[...] yerine tip'li okuma).
public class SagaAuth
{
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
}
