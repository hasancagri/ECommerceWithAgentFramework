# Research: Ürün Yorumları ve Puanlama (044)

## R1 — "Tamamlanmış sipariş" = OrderStatus.Confirmed

- **Decision**: Satın-alma şartı `OrderStatus.Confirmed` siparişe bakar.
- **Rationale**: Kodda durumlar Pending/Confirmed/Cancelled (`Order.cs`); teslimat/iade akışı yok,
  Confirmed = başarılı terminal. Spec'teki "Completed" iş dilidir, koda Confirmed düşer.
- **Alternatives**: Yeni Completed durumu eklemek — kapsam dışı (kargo feature'ı ile anlamlı).

## R2 — Satın-alma kanıtı kanalı: senkron gRPC (event-projeksiyon değil)

- **Decision**: `Shared/Protos/order_purchase.proto` — Order sunucu, Reviews istemci;
  `HasConfirmedPurchase(user_id, product_id) → bool`. Fail-closed (FR-008).
- **Rationale**: Anlık evet/hayır kararı — anayasa İlke I'in senkron RPC tanımına birebir (012
  emsali). Alternatif (OrderConfirmed event + Reviews'ta purchase read-model) yeni event kontratı +
  geçmiş siparişlerin backfill'i + çift veri ister; tek soru için ağır.
- **Alternatives**: (a) Integration event + lokal projeksiyon — backfill/çift veri yükü, ELENDİ;
  (b) REST çağrısı — gRPC emsali (proto altyapısı, BearerForwardingHandler) hazır, ELENDİ.

## R3 — Özet dağıtımı: fat event `ReviewSummaryChanged`

- **Decision**: Yorum eklendiğinde/gizlendiğinde Reviews özeti yeniden hesaplar ve
  `ReviewSummaryChanged(ProductId, Average, Count)` yayınlar; Storefront satırına yazar.
- **Rationale**: Writer-publishes fat event ilkesi (003/006); Storefront pull-back yapmaz.
  Ortalama Reviews'ta hesaplanır — tüketici sayı saymaz (SC-003 tek kaynaktan).
- **Alternatives**: Storefront'un Reviews API'sinden çekmesi — push-only read model ilkesini bozar.

## R4 — Order gRPC ucunun yetkisi: kullanıcı bearer + `order.read` scope YOK, `reviews` özel scope YOK

- **Decision**: Uç, kullanıcı token'ı ile çağrılır (BearerForwardingHandler emsali) ve token'daki
  `sub` ile istenen `user_id` eşleşmek zorundadır; scope olarak mevcut `order.read` KULLANILMAZ,
  yeni dar scope da AÇILMAZ — uç `basket.write` benzeri ayrı scope yerine `reviews.write` ister
  (çağıran zaten o scope'la yazma akışındadır). KnownScopes'a yalnız `reviews.write` eklenir.
- **Rationale**: Tek yeni scope; uç yalnız "kendi satın-almam var mı" sorusuna cevap verir
  (sub eşleşme guard'ı) — başkasının sipariş verisi sızmaz. 012'de gRPC ucu tek scope ister
  (`stock.reserve`) — aynı desen.
- **Alternatives**: Ayrı `order.purchase-check` scope'u — scope enflasyonu, ELENDİ.

## R5 — ModerationAgent: MAF ChatClientAgent, 041 EnrichmentAgent emsali

- **Decision**: In-process Singleton `ModerationAgent` (ChatClientAgent, Temperature=0, structured
  JSON, MCP'siz). Çıktı: `{ violation: bool, category: enum, reason: string }`. Lokal durable
  kuyruk `reviews.moderate`; retry 10s/30s/60s → error queue. `ModerationOptions` fail-fast.
- **Rationale**: Kullanıcı kararı ("kontrol Agent üzerinden") + kurulu emsal (EnrichmentAgent).
  Kelime listesi Türkçe hakaret varyasyonlarını yakalayamaz. Agent yalnız KARAR verir; gizlemeyi
  `Review.Hide(verdict)` aggregate metodu uygular (İlke II guard).
- **Alternatives**: (a) Senkron denetim (yayın öncesi) — yazma gecikmesi + OpenAI kesintisinde
  yazma durur, ELENDİ (FR-012 fail-open); (b) kelime listesi — yetersiz, ELENDİ;
  (c) OpenAI Moderation API — ayrı sağlayıcı yüzeyi, MAF emsali dışına çıkar, ELENDİ (aday not).

## R6 — Özet Storefront'ta satır alanı (ayrı read-model tablosu değil)

- **Decision**: `StorefrontView` satırına `RatingAverage (decimal?)` + `RatingCount (int)` eklenir;
  `ApplyReviewSummary` metodu yazar. Null/0 = rozet çizilmez (FR-006).
- **Rationale**: Stok/fiyat ile aynı desen — composite satır tek kaynaktan çizilir; ayrı tablo
  join/ikinci sorgu getirir.
- **Alternatives**: WebApp'in detayda Reviews API'den özet çekmesi — kart listesi için N+1, ELENDİ
  (detay sayfası yorum LİSTESİNİ Reviews API'den çeker, özet karttan gelir).

## R7 — Ad maskeleme: yazım anında ham ad + görüntülemede maske

- **Decision**: Review, kullanıcının görünen adını (token claim'inden) ham saklar; API yanıtı
  `MaskedName` döner (VO `ReviewerName.Masked()` — "Hasan Demiriz" → "H** D**"). Ham ad yüzeye çıkmaz.
- **Rationale**: Maske kuralı değişirse geçmiş yorumlar yeniden maskelenebilir (görüntüleme kuralı,
  veri kuralı değil). Tek harf/boş ad kenar durumları VO'da test-first.
- **Alternatives**: Maskeyi yazımda sabitlemek — kural değişiminde geçmiş tutarsız, ELENDİ.

## R8 — Yorum listesi WebApp'e Reviews API'den (gateway route), sayfalı

- **Decision**: WebApp detay sayfası yorumları `GET /api/v1/reviews/products/{productId}` (anonim,
  sayfalı, en yeni üstte; Hidden hariç) ucundan Refit ile çeker; gateway'e `/reviews` route eklenir.
- **Rationale**: Mevcut WebApp→servis deseni (Refit + Aspire discovery). Hidden filtreleme sunucuda.
- **Alternatives**: Yorumları Storefront'a denormalize etmek — liste büyük/sayfalı, read-model
  satırı şişer; özet-satır deseninin amacı dışında, ELENDİ.

## R9 — Tek-yorum kilidi: aggregate kimliği `UserId+ProductId` bileşik anahtar

- **Decision**: `Review.Id = Guid` kalır; teklik Marten `UniqueIndex(UserId, ProductId)` ile
  (SpecificationAttribute.NormalizedName emsali). İhlal handler'da yakalanıp
  `REVIEW_ALREADY_EXISTS` Result'ına çevrilir.
- **Rationale**: At-least-once/yarış durumunda DB kilidi son sözü söyler; uygulama kontrolü
  (önce sorgula) + unique index birlikte (çift savunma).
- **Alternatives**: Yalnız uygulama kontrolü — yarışta çift yorum, ELENDİ.