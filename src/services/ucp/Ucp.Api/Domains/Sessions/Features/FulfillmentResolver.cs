namespace Ucp.Api.Domains.Sessions.Features;

/// <summary>
/// Basit mağaza kargo mantığı (karmaşık tarife motoru DEĞİL — R5/spec assumption). Kitapyurdu deseni:
/// eşik üstü ücretsiz. Seçenekler adrese göre sunulur; seçilen seçeneğin bedeli totals'a yansır.
/// Tutarlar minor units (TRY kuruş).
/// </summary>
public static class FulfillmentResolver
{
    public record Option(string Id, string Label, long CostMinor);

    // 150 TRY üstü kargo ücretsiz (subtotal minor >= 15000).
    private const long FreeShippingThresholdMinor = 15000;
    private const long StandardCostMinor = 2999;   // 29,99 TRY
    private const long ExpressCostMinor = 4999;     // 49,99 TRY

    /// <summary>Verilen subtotal'a göre sunulan kargo seçenekleri (standart eşik üstü ücretsiz).</summary>
    public static IReadOnlyList<Option> OptionsFor(long subtotalMinor)
    {
        var standard = subtotalMinor >= FreeShippingThresholdMinor ? 0 : StandardCostMinor;
        return
        [
            new Option("standard", "Standart Kargo (2-4 iş günü)", standard),
            new Option("express", "Hızlı Kargo (1-2 iş günü)", ExpressCostMinor)
        ];
    }

    /// <summary>Seçilen seçenek id'sini çözer → UcpFulfillment; bilinmeyen seçenek Error.</summary>
    public static ResultDomain<UcpFulfillment> Resolve(
        string selectedOptionId, string destinationId, long subtotalMinor)
    {
        var option = OptionsFor(subtotalMinor).FirstOrDefault(o => o.Id == selectedOptionId);
        if (option is null)
            return ResultDomain<UcpFulfillment>.Error(new MessageItem { Code = UcpResourceConstants.FULFILLMENT_OPTION_INVALID });

        return UcpFulfillment.Create("shipping", option.Id, option.Label, option.CostMinor, destinationId ?? "");
    }
}