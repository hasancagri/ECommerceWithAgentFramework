namespace Customer.Api.Infrastructure.PaymentGateway;

// 075: PG kart-sözleşmesi portu (mağaza → PG REST). sağlayıcı PG'nin İÇİNDE (FR-016) — mağaza yalnız bu
// sözleşmeyi bilir. Kart verisi (PAN/CVV) mağaza sınırından içeri HİÇ girmez; mağaza opak handle'ları
// ve gösterilebilir izdüşümü görür. Sözleşme: contracts/pg-card-contract.md.
public interface IPgCardClient
{
    /// <summary>Hosted kart-ekleme oturumu başlatır. PG Checkout Form'u nominal doğrulamayla
    /// açar; tarayıcıda açılacak <c>addUrl</c>'i döner. conversationId mağazanın tek-kullanımlık
    /// session kimliğidir (callback korelasyonu — R3). Başarısız/erişilemezse Success=false.</summary>
    Task<StartAddSessionResult> StartAddSessionAsync(Guid conversationId, CancellationToken ct);

    /// <summary>Kullanıcı formu bitirince sonucu PG'den çeker (pull). success → pgUserHandle döner;
    /// pending → henüz tamamlanmadı; failure/cancelled → kalıcı kayıt yapılmaz (FR-010).</summary>
    Task<CompleteAddResult> CompleteAddAsync(Guid conversationId, CancellationToken ct);

    /// <summary>Kullanıcının saklı kartlarının gösterilebilir izdüşümünü canlı listeler (PAN/CVV yok).
    /// PG erişilemezse null (getirilemiyor — bayat kopya gösterme).</summary>
    Task<IReadOnlyList<PgCard>?> ListCardsAsync(string userHandle, CancellationToken ct);

    /// <summary>Kullanıcının kartını PG'den siler. Yalnız o userHandle'ın kartı silinir.</summary>
    Task<bool> DeleteCardAsync(string userHandle, string cardHandle, CancellationToken ct);
}

// PG add-session sonucu (Success=false → oturum açılamadı/PG erişilemez).
public sealed record StartAddSessionResult(bool Success, string? AddUrl);

// PG complete/poll durumu (R3).
public enum PgAddStatus { Pending = 0, Success = 1, Failure = 2 }

public sealed record CompleteAddResult(PgAddStatus Status, string? PgUserHandle);

// Kart gösterilebilir izdüşümü (opak cardHandle + görünür alanlar; PAN/CVV ASLA).
public sealed record PgCard(
    string CardHandle, string Brand, string Last4, int ExpiryMonth, int ExpiryYear, string? Alias);
