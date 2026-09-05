namespace Order.Api.Options;

// 039: correlation-key HMAC sunucu secret'i — section "CorrelationKeyOption". CorrelationKey.Create
// bu secret ile HMAC uretir; girdiler bilinse bile secret olmadan anahtar forge edilemez (R2/R5).
// VO'nun kendisi config bilmez — secret handler'dan gecirilir (VO saf kalir).
public class CorrelationKeyOption
{
    [Required] public string ServerSecret { get; set; } = "";
}
