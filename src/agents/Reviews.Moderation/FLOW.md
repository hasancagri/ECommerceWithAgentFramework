# Reviews.Moderation — Domain Süreci

**BC ne yapar:** Reviews'ten gelen moderasyon isteğini dinler, yorum metnini AI ile denetler,
kapalı bir kategori kümesinde **karar** verir ve kararı Reviews'e geri yayınlar. DB'siz worker.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Moderasyon isteği tüketilir.** Reviews yeni yorumda           `(ReviewModerationRequested`
   `ReviewId` + metin + yıldız yollar; PII (ad/UserId) YOK.          ` → ReviewModerationEventHandlers)`
2. **Metinsiz istek temiz sayılır.** Savunma: metin boşsa LLM'e     `(→ ReviewModerated(false,"none"))`
   gitmeden `violation=false` yayınlanır (normalde gelmez).
3. **Yorum AI denetçisine verilir.** Yalnız metin + yıldız gider;   `(ModerationAgent.ModerateAsync)`
   Singleton `ChatClientAgent`, Temp=0, MCP'siz, structured JSON.
4. **Karar kapalı kategoriye düşer.** İhlal iken kategori           `(ModerationAgent.ModerationOutput)`
   profanity/insult/personal_attack; temizde "none".
5. **Şema-dışı karar reddedilir.** İhlal ama kategori boş/"none"    `(→ ModerationException)`
   ise savunma fırlatır → retry yolu.
6. **Karar Reviews'e geri yayınlanır.** İhlal + kategori + kısa     `(→ ReviewModerated)`
   gerekçe; uygulama (gizleme) Reviews domain'inde.                 `(Review.ApplyModeration)`
7. **LLM hatası dayanıklı yönetilir.** Fail-open: retry 10s/30s/60s `(ModerationException → DLQ)`
   sonra error queue; yorum Reviews'te görünür kalır.

## Domain kuralları (süreci yöneten değişmezler)

- **DB'siz worker.** Kendi durumu yok; girdi/çıktı yalnız event. Kalıcılık Reviews BC'nin sorumluluğu.
- **Sözleşmede PII yok.** `ReviewModerationRequested` yalnız `ReviewId` + metin + yıldız taşır; ad/UserId ASLA.
- **Kapalı kategori kümesi.** İhlal = profanity/insult/personal_attack; temiz = none. Şema-dışı karar reddedilir.
- **Agent yalnız KARAR verir, Singleton.** Gizleme yok; uygulama Reviews'te (`Review.ApplyModeration`).
- **Fail-open + retry→DLQ.** LLM hatası ihlal saymaz; `ModerationException` → cooldown retry → error queue.

## Sınır (bu BC'nin dokunmadığı)

Yorum kalıcılığı, görünürlük/gizleme, satın-alma şartı, özet yayını yok — hepsi Reviews BC'de. AI kimlik üretmez.
