# Data Model: Reviews Moderasyon Agent Taşıma

Yeni **kalıcı** varlık yok (worker DB'siz; Reviews aggregate/şema değişmez). Bu dosya yalnız
mesaj sözleşmelerini + değişmeyen domain varlıklarının ilgili yüzeyini sabitler.

## Integration Events (yeni — `Shared.IntegrationEvents`)

### ReviewModerationRequested
Yayıncı: Reviews.Api · Tüketici: Reviews.Moderation worker

| Alan | Tip | Not |
|---|---|---|
| ReviewId | Guid | Korelasyon anahtarı (sonuç bununla geri bağlanır) |
| Text | string | Yorum metni (moderasyona girer). **Boş yayınlanmaz** (FR-010) |
| Rating | int | Yıldız (1–5), yalnız bağlam |

- **PII yok** (FR-009): UserId / kullanıcı adı ASLA taşınmaz.
- Additive alan gelecekte default'lu eklenir (eski tüketici kırılmaz).

### ReviewModerated
Yayıncı: Reviews.Moderation worker · Tüketici: Reviews.Api

| Alan | Tip | Not |
|---|---|---|
| ReviewId | Guid | Hangi yoruma ait |
| Violation | bool | true = ihlal (gizlenecek) |
| Category | string | Kapalı küme: profanity / insult / personal_attack / none |
| Reason | string | ≤200 karakter kısa gerekçe (iz; yüzeye çıkmaz) |

## Worker İç Sözleşmesi (kalıcı değil)

**ModerationOutput** (agent structured JSON çıktısı, worker-içi): `{ bool Violation, string Category,
string Reason }`. Kapalı kategori enum'u; şema-dışı çıktı = hata → retry → error queue (FR-014).
`ReviewModerated` event'ine birebir map'lenir.

## Değişmeyen Domain (referans)

### Review (aggregate — DEĞİŞMEZ)
- `Status`: `ReviewStatus { Visible=1, Hidden=2 }` — Hidden terminal.
- `ApplyModeration(ModerationVerdict verdict, DateTimeOffset now) : ResultDomain` — ihlalde Hidden,
  temizde damgalar; tekrar çağrı no-op (idempotent, `ModeratedAtUtc` ile).
- Teklik: Marten `UniqueIndex(UserId, ProductId)`.

### ModerationVerdict (VO — DEĞİŞMEZ)
- `Create(bool violation, string category, string reason) : ResultDomain<ModerationVerdict>` —
  kapalı kategori doğrulaması burada. Worker'dan gelen `ReviewModerated` alanları bununla üretilir;
  şema-dışı gelirse handler hata verir (savunma).

## Durum Akışı (uçtan uca)

```
SubmitReview (purchase-check OK) → Review(Visible) reviewsDb'ye yaz
   ├─ ReviewSummaryChanged yayınla (mevcut)
   └─ metin varsa: ReviewModerationRequested yayınla (outbox)         [Reviews→broker]
                         ↓
        worker: ModerateAsync(text, rating) → ModerationOutput         [Reviews.Moderation]
                         ↓
                 ReviewModerated yayınla                                [worker→broker]
                         ↓
   Reviews tüketir: Review yükle → ApplyModeration(verdict)            [Reviews]
      └─ Visible→Hidden olduysa: özet yeniden hesapla + ReviewSummaryChanged yayınla
```

- Idempotent: `ReviewModerated` iki kez gelirse ikinci `ApplyModeration` no-op (FR-011).
- Bilinmeyen/silinmiş ReviewId: sessiz no-op.
