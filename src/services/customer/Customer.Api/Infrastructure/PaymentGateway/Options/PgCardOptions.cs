using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Infrastructure.PaymentGateway.Options;

/// <summary>
/// 075: PG kart-saklama sözleşmesi bağlantı config'i — appsettings section <c>PgCardOptions</c>.
/// Mağaza PG'nin kart uçlarını (add-session / list / delete) bu taban adres + göreli yollarla tüketir;
/// sağlayıcı PG'nin İÇİNDE (FR-016) — mağaza yalnız bu sözleşmeyi bilir. Kart verisi mağazada tutulmaz.
/// Magic-string config[...] yerine tip'li okuma (Options pattern; İLKE III kod standardı).
/// </summary>
public class PgCardOptions
{
    /// <summary>PG kart uçlarının taban adresi (host). Ör. https://dropshop-payment.</summary>
    [Required] public string BaseUrl { get; set; } = "";

    /// <summary>Hosted kart-ekleme oturumu açan uç (POST). Yanıt: addUrl + conversationId.</summary>
    public string CardSessionsPath { get; set; } = "vault/card-sessions";

    /// <summary>Kart listeleme/silme uçlarının göreli yolu (GET ?userHandle= / DELETE).</summary>
    public string CardsPath { get; set; } = "vault/cards";

    /// <summary>
    /// PG'nin kullanıcı hosted-form'u bitirince mağazaya döneceği callback adresi (add-session'a gider).
    /// Boşsa PG pull moduna düşer (mağaza conversationId ile sorar). Tam URL (gateway'den erişilebilir).
    /// </summary>
    public string CallbackUrl { get; set; } = "";
}