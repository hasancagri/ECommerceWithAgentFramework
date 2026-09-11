namespace Ucp.Api.Options;

/// <summary>
/// UCP kanalının platform-kimliği + sunum config'i (appsettings section <c>UcpPlatform</c>). Keşif
/// profili + giden webhook hedefi + ödeme handler ilanı buradan beslenir. Magic-string <c>config[...]</c>
/// yerine tip'li okuma (İlke: Options pattern). Anahtar materyali BURADA DEĞİL (<see cref="UcpSigningOption"/>).
/// </summary>
public class UcpPlatformOption
{
    /// <summary>Mağazanın UCP profil URL'i — giden webhook <c>UCP-Agent</c> header'ı + keşifte self-referans.</summary>
    [Required] public string StoreProfileUrl { get; set; } = "";

    /// <summary>
    /// Platform bildirim alıcısı (webhook inbox). MVP'de tek konfigüre hedef (simülatör <c>/inbox</c>);
    /// gerçek UCP'de session başına <c>UCP-Agent</c> profilinden çözülür — sonraki feature.
    /// </summary>
    [Required] public string WebhookInboxUrl { get; set; } = "";

    /// <summary>Profilde ilan edilen ödeme handler kimliği (mağaza/PG kabul ettiği yöntem — FR-016).</summary>
    [Required] public string PaymentHandlerSpec { get; set; } = "";

    /// <summary>
    /// Sandbox ödeme aracı referansı — complete'te <c>ucp_payment_ref</c> olarak Order'a taşınır (iyzico
    /// sandbox test instrument'ı; dış alıcının vault kartı yok — R7). Gerçek para yok.
    /// </summary>
    [Required] public string PaymentInstrument { get; set; } = "";

    /// <summary>Session son-kullanma süresi (saat); belirtilmezse UCP varsayılanı 6 (FR-008).</summary>
    [Range(1, 168)] public int SessionTtlHours { get; set; } = 6;
}