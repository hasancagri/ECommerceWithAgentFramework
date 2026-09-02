namespace Shared;

public static class IntegrationEvents
{
    // 028: OrderCreatedEvent kaldirildi — sepet temizligi CheckoutSaga'nin gRPC adimina tasindi.

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    // 006-home-storefront-list: Description/Price/Brand eklendi.
    // 016-category-brand: kimlik + ad birlikte taşınır (R7); Id opak değerdir, tüketici lookup yapmaz.
    // Kategori zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün domain'de yoktur.
    // 043: Specs — kanonik özellik AD çiftleri (Id taşınmaz; sözleşme=AD, taksonomi deseni).
    // Additive + opsiyonel: eski yayıncı/tüketici kırılmaz; null = özellik bilgisi yok (boş sayılır).
    public record ProductSpec(string Attribute, string Option);

    // 052: kitap künyesi — yazar (Id+ad çifti; paralel-liste kırılganlığı olmadan taşınır).
    public record AuthorRef(Guid Id, string Name);

    // 052: kırıcı evrim (tek tüketici Storefront, aynı PR, DB sıfırdan seed). BrandId/Brand çıktı;
    // çok-yazar (Authors) + tek yayınevi (PublisherId+Publisher, fat: tüketici lookup yapmaz) geldi.
    public record ProductChangedEvent(
        Guid ProductId,
        string Name,
        string Description,
        decimal Price,
        List<AuthorRef> Authors,
        Guid PublisherId,
        string Publisher,
        Guid CategoryId,
        string Category,
        string? ImageUrl,
        bool IsDeleted,
        List<ProductSpec>? Specs = null,
        // 045: varyant ailesi kodu (opsiyonel; null = ailesiz).
        string? FamilyCode = null,
        // 060: YALNIZ fiyat değişiminde dolu (eski fiyat); null = fiyat-dışı değişiklik.
        // Additive default'lu — eski tüketici (Storefront) kırılmaz. Library tetik kararını bundan verir.
        decimal? OldPrice = null);
    public record StockChangedEvent(Guid ProductId, int Quantity);


    // 050/051: Catalog → Stock. Yalnız YENİ ürün YAYINLANINCA yayılır; Stock BarcodeLink eşlemesini kurar
    // ve OnHand'i InitialStock ile mutlak yazar. 051: ilk yayıncısı = kitap import ("Linked" feed-adı düştü).
    public record ProductAdded(
        string Barcode,
        Guid ProductId,
        int InitialStock);

    // 044: Reviews → Storefront. Visible yorumlardan MUTLAK özet (delta değil) — geç/yeniden teslim
    // son-yazan-kazanır ile güvenli. Count=0 ⇒ tüketici özeti temizler (rozet çizilmez).
    public record ReviewSummaryChanged(Guid ProductId, decimal Average, int Count);

    // 046: Reviews → Reviews.Moderation worker. Moderasyon istegi; PII YOK (yalniz metin+yildiz+id).
    public record ReviewModerationRequested(Guid ReviewId, string Text, int Rating);

    // 046: Reviews.Moderation worker → Reviews. Moderasyon karari; kategori kapali kume
    // (profanity/insult/personal_attack/none). Reviews ApplyModeration ile uygular.
    public record ReviewModerated(Guid ReviewId, bool Violation, string Category, string Reason);

    // Order → Reviews. YALNIZ odeme onayli tamamlanan siparis (Confirm pivotu) icin yayilir;
    // olusturulan/odenmemis DEGIL. Reviews satin-alma kanitini (yorum hakki) bu event'ten projeksiyonlar.
    // Category/Brand nullable: Order bunlari tutmuyorsa null (BC izolasyonu).
    // Additive: yeni alan default'lu eklenir, eski tuketici kirilmaz.
    public record OrderCompleted(
        Guid OrderId,
        Guid UserId,
        DateTimeOffset OrderedAt,
        IReadOnlyList<OrderCompletedItem> Items);

    public record OrderCompletedItem(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice,
        string? Category = null,
        string? Brand = null);

    // 060: Library → NotificationAgent. Üründeki HER alarm için bir event (alarm açık kalır — yaşayan
    // abonelik, FR-004). Mail'e yetecek her alan event'te; worker başka servise SORMAZ (email snapshot).
    public record PriceAlarmTriggered(
        Guid AlarmId,
        Guid UserId,
        string Email,
        Guid ProductId,
        string ProductName,
        decimal OldPrice,
        decimal NewPrice);

    // 060: NotificationAgent → Library. Gönderim denemesinin sonucu; Library NotificationRecord izi yazar.
    // Detail: "sent" | "no-email" | kısa hata özeti.
    public record NotificationSent(
        Guid UserId,
        Guid ProductId,
        string Email,
        bool Success,
        string Detail);
}