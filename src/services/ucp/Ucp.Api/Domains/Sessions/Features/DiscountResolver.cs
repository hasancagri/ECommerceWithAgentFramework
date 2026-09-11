namespace Ucp.Api.Domains.Sessions.Features;

/// <summary>
/// Basit kod-tabanlı indirim (promosyon motoru DEĞİL — R6/spec assumption). Geçerli kod totals'a yansır;
/// geçersiz/uygunsuz kod açıklayıcı mesajla reddedilir, session hataya DÜŞMEZ (FR-018). Tutarlar minor.
/// </summary>
public static class DiscountResolver
{
    public record Resolved(List<UcpAppliedDiscount> Applied, List<MessageItem> Messages);

    // Sabit demo kod tablosu (sandbox). Yüzde kodları subtotal'a oranlı; sabit kodlar düz tutar.
    private static readonly Dictionary<string, (string Title, int Percent, long FixedMinor)> Codes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["BOOK10"] = ("Kitap %10 indirim", 10, 0),
            ["WELCOME"] = ("Hoş geldin 50 TL", 0, 5000)
        };

    /// <summary>Kodları çözer; geçerlileri applied, geçersizleri messages. Boş/null kod listesi = temizle.</summary>
    public static Resolved Resolve(IReadOnlyList<string>? codes, long subtotalMinor)
    {
        var applied = new List<UcpAppliedDiscount>();
        var messages = new List<MessageItem>();
        if (codes is null) return new Resolved(applied, messages);

        foreach (var code in codes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Codes.TryGetValue(code, out var rule))
            {
                messages.Add(new MessageItem { Code = UcpResourceConstants.DISCOUNT_CODE_INVALID });
                continue;
            }

            var amount = rule.Percent > 0
                ? (long)Math.Round(subtotalMinor * rule.Percent / 100m, MidpointRounding.AwayFromZero)
                : rule.FixedMinor;

            // Subtotal 0 iken yüzde indirim 0 olur → anlamsız; uygulama.
            if (amount <= 0)
            {
                messages.Add(new MessageItem { Code = UcpResourceConstants.DISCOUNT_CODE_INVALID });
                continue;
            }

            var created = UcpAppliedDiscount.Create(rule.Title, amount, code);
            if (created.IsSuccess) applied.Add(created.Data!);
            else messages.AddRange(created.Messages ?? []);
        }

        return new Resolved(applied, messages);
    }
}