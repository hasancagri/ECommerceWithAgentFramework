# Reviews — Domain Süreci

**BC ne yapar:** Satın-alma şartıyla ürün yorumu toplar; yorum HEMEN görünür doğar, AI moderasyonu
AYRI worker'da async koşar, ihlalde yorumu gizler ve vitrin özetini Storefront'a yayınlar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Yorum yalnız satın-alanlar tarafından yazılır.** Order'a           `(OrderPurchaseClientProxy`
   senkron gRPC ile kanıt sorulur; kanal yoksa fail-closed RED.          ` → HasConfirmedPurchaseAsync)`
2. **Kullanıcı × ürün için tek yorum.** Uygulama önce kontrol eder,     `(SubmitReviewCommandHandler)`
   son sözü Marten unique index söyler (yarış kaybedeni nazik hata).
3. **Yorum Visible durumda doğar** — puan 1-5 tam, metin ≤2000, ad      `(Review.Create)`
   zorunlu; görünen ad token claim'inden, istek gövdesinden ASLA.
4. **Görünen ürün özeti anında yayınlanır.** Ortalama + adet            `(ReviewSummaryChanged)`
   Reviews'ta hesaplanır (tüketici saymaz), Storefront'a fanout.
5. **Metinli yorum için moderasyon istenir.** Yalnız metin varsa;       `(ReviewModerationRequested)`
   id + metin + yıldız (PII yok). Metinsizde denetlenecek içerik yok.
6. **Moderasyon AYRI worker'da koşar** (Reviews'te agent-framework       `(reviews-moderation-agent)`
   YOK). LLM kararı `ReviewModerated` ile geri döner.
7. **Karar uygulanır: ihlalde gizle, temizde yalnız damgala.**          `(Review.ApplyModeration)`
   Denetim tamamlanmışsa ikinci karar no-op (at-least-once idempotent).
8. **Gizlenen yorumda özet MUTLAK yeniden yayınlanır.** Visible→Hidden   `(ReviewsEventHandlers`
   olduysa yeni ortalama/adet; Count=0 ⇒ tüketici temizler.               ` → ReviewSummaryChanged)`

## Domain kuralları (süreci yöneten değişmezler)

- **Satın-alma şart (fail-closed).** Kanıt kanalı erişilemezse yorum reddedilir; "kanıt yok" sayılmaz.
- **Fail-open moderasyon.** Yorum Visible doğar; denetim beklerken/şema-dışı kararda görünür kalır.
- **Hidden terminaldir.** İtiraz/geri alma v1 dışı; ikinci moderasyon kararı durumu değiştirmez.
- **Agent yalnız KARAR verir.** Gizleme domain'de (`Review.ApplyModeration`); `ModerationVerdict` guard'lı.
- **Ham ad saklanır, yüzeye Masked() çıkar** (`ReviewerName`) — maske görüntüleme kuralı, veri değil.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat, sipariş, ödeme yok. LLM/agent-framework Reviews'ta YOK — moderasyon ayrı
`reviews-moderation-agent` worker'ında; iletişim yalnız event (`ReviewModerationRequested`/`ReviewModerated`).
