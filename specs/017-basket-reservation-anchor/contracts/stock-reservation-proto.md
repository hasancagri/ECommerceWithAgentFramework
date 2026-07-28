# Contracts: 017 Basket Reservation Anchor

İki kontrat yüzeyi değişir: paylaşılan gRPC proto'su ve Basket REST/Agent sepet sorgusu.

## 1. gRPC — `Shared/Protos/stock_reservation.proto` (v2, geriye uyumlu)

Değişen tek mesaj:

```proto
message SetReservedQuantityRequest {
  string product_id = 1;   // Guid
  string user_id = 2;      // Guid (sepet sahibi)
  int32 quantity = 3;      // hedef adet; 0 => release
  // 017: opsiyonel mutlak bitiş (ISO-8601 "O", UTC). Boş/verilmemiş => sunucu sabit TTL uygular.
  // Verilmişse rezervasyon bu mutlak zamanla yaratılır; mevcut rezervasyonun bitişi buna eşitlenir.
  string expires_at = 4;
}
```

Kurallar:

- Alan eklemesi wire-uyumludur; eski istemciler (Order.Api) alanı yazmaz → boş string → sabit TTL (FR-006, FR-014).
- `Release`, `Commit`, `ReservationReply`, `ReservationStatus` DEĞİŞMEZ.
- Sunucu boş olmayan ama parse edilemeyen `expires_at`'i yok sayar (sabit TTL'e düşer) — savunmacı, yeni hata kodu yok.
- Geçmiş bir `expires_at` reddedilmez; rezervasyon doğar ve ilk sweep'te dolar (çağıran çapayı doğru yönetir).

İstemci tarafı (Basket.Api):

- `StockReservationClientProxy.SetReservedQuantityAsync(productId, userId, quantity, expiresAt, ct)` —
  `expiresAt` her çağrıda sepet çapası (aday ya da mevcut) olarak geçilir; `"O"` formatıyla serileştirilir.
- Order.Api istemcisi DEĞİŞMEZ.

## 2. REST — `GET /v1/basket/user` (GetBasket response)

```jsonc
{
  "userId": "…",
  "items": [
    {
      "id": "…", "name": "…", "imageUrl": "…",
      "price": 10.0, "priceByApplyDiscountRate": null,
      "quantity": 2
      // "reservationExpiresAt" KALKTI (FR-009)
    }
  ],
  "discountRate": null,
  "coupon": null,
  "totalPrice": 20.0,
  "totalPriceWithAppliedDiscount": null,
  "reservationExpiresAt": "2026-07-28T12:05:00.0000000+00:00", // YENİ: sepet çapası; null = çapa yok
  "isReservationExpired": false                                 // YENİ: türetilmiş (FR-010)
}
```

- `reservationExpiresAt` null ise UI banner göstermez (eski/boş sepetler).
- `isReservationExpired == true` iken banner "Expired" gösterir; checkout bloklanmaz (FR-012).
- Item'daki `reservationExpiresAt` alanının kaldırılması kırıcıdır; tek tüketici WebApp aynı PR'da güncellenir.

## 3. Agent yüzeyi — `Features/Agent/GetBasket` (MCP)

- Response'a REST ile aynı iki sepet düzeyi alan eklenir: `reservationExpiresAt`, `isReservationExpired`.
- Agent slice'ları ince kalır; alanlar aggregate'ten okunur, iş mantığı eklenmez.