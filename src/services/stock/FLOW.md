# Stock — Domain Süreci

**BC ne yapar:** Her ürünün fiziksel stoğunun (**OnHand**) tek otoritesidir. Catalog'un ürün-bağ olayından
ilk OnHand'i yazar, sepet için TTL'li rezervasyon tutar, sipariş anında rezervasyonu kalıcı düşüşe çevirir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Feed (Procurement) söküldü; OnHand'i besleyen kanonik-ürün olayı kalktı.
> İlk stok `ProductAdded` ile gelir (051: ilk yayıncı kitap import); sonraki güncellemeler ürün-CRUD yoluyla.

## Süreç

1. **Ürün ilk eşlendiğinde barkod↔ProductId eşlemesi kurulur** ve      `(StockEventHandlers`
   OnHand başlangıç değeriyle yazılır (idempotent upsert).             ` → ProductAdded)`
2. **Stok her değişiminde vitrine bildirilir** — Storefront read-      `(IntegrationEvents`
   model'i güncel OnHand'i alır.                                       ` .StockChangedEvent)`
4. **Sepete ekleme TTL'li rezervasyon tutar** (gRPC, fail-closed).     `(ReserveStock`
   Available = OnHand − aktif rezervasyonlar; yetmezse reddedilir.     ` → ProductStock.SetReservedQuantity)`
5. **Rezervasyon bitiş anına dayanıklı süre-sonu tetiği kurulur.**     `(SweepReservation`
   Tam o an düşer; polling penceresi yok, restart'a dayanır.          ` ← bus.ScheduleAsync)`
6. **Sepetten çıkarma rezervasyonu bırakır** (idempotent no-op).       `(ProductStock.Release)`
7. **Süresi geçen rezervasyon serbest bırakılır + Basket'e bildirilir.** `(ProductStock.PurgeExpired`
   Sepet satırı silinir; aktif/yenilenmiş rezervasyon korunur.        ` → ReservationExpired)`
8. **Sipariş rezervasyonu kalıcı stok düşüşüne çevirir** (gRPC).       `(CommitStock`
   orderId ile idempotent; OnHand düşer, hold kapanır.                ` → ProductStock.Commit)`
9. **Saga iptalinde commit edilmiş adet stoğa geri eklenir** (telafi). `(RevertCommitStock`
   Yalnız daha önce commit edilmiş sipariş geri alınabilir.           ` → ProductStock.RevertCommit)`

## Domain kuralları (süreci yöneten değişmezler)

- **OnHand otoritesi = Stock (050).** İlk OnHand `ProductAdded`'ten mutlak yazılır; sonraki güncelleme ürün-CRUD yazım yoluyla. Negatif reddedilir `(ProductStock.SetQuantity)`.
- **Available türetilir, OnHand'i ezmez.** Available = OnHand − aktif rezervasyonlar, 0'a kırpılır `(ProductStock.AvailableAt)`; oversell tespit edilir `(ProductStock.IsOversoldAt)`.
- **Rezervasyon fail-closed + sabit TTL.** Yetersiz stokta gRPC reddeder; ExpiresAt yenilenmez (rolling-TTL yok).
- **Commit/Revert idempotent (028).** orderId anahtarıyla mükerrer teslimat no-op; commit'siz revert reddedilir `(_processedOps)`.
- **İş mantığı aggregate'te.** gRPC sunucu ince sarıcı `(StockReservationGrpcService)`; yalnız `IMessageBus` command'ına devreder.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/kimlik/fiyat üretmez (Catalog otoritesi). Sepet/sipariş/ödeme sahibi değil — yalnız
rezervasyon + commit hizmeti verir. Fiziksel depo/lojistik kapsam dışı.
