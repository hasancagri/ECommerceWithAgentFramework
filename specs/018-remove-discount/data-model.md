# Data Model: Discount'ın Sistemden Tamamen Kaldırılması

Yeni entity yoktur; mevcut modellerden alan/davranış silinir. Aşağıda her modelin SON hali özetlenir.

## Silinen modeller

- **Discount (aggregate, Discount BC)**: `DiscountCode` + `DiscountRate` VO'larıyla birlikte tamamen silinir.
- **Discount (value object, Basket BC)**: `Coupon` + `Rate` taşıyan VO silinir.
- **DiscountWriterResult (IngestionAgent)**: workflow adım sonucu silinir.
- **discountDb / `discountManagement` şeması**: veritabanı ve şema tanımı düşer.

## Değişen modeller

### Basket (Basket BC — aggregate)

- Kalkan: `AppliedDiscount`, `IsApplyDiscount()`, `GetTotalPriceWithAppliedDiscount()`,
  `ApplyNewDiscount()`, `ApplyAvailableDiscount()`, `ClearDiscount()`.
- Kalkan (BasketItem): `PriceByApplyDiscountRate`, `ApplyDiscount()`, `ClearDiscount()`.
- Kalan invariant'lar: satır tekilleştirme, adet artırma, `GetTotalPrice()` = Σ(birim fiyat × adet).
- Not: `BASKET_IS_EMPTY` kuralının tek kullanıcısı kupon uygulamaydı; slice'la birlikte kullanım kalkar.

### Order (Order BC — aggregate)

- Kalkan: `DiscountRate` alanı; `Create(...)` imzasından `discountRate` parametresi düşer.
- Kalan: alıcı, adres, satırlar, toplam; davranışlar değişmez.

### StorefrontView (Storefront BC — read model)

- Kalkan: `DiscountRate` alanı, `ApplyDiscount(rate)` metodu.
- Kalan: Catalog (ad/fiyat/görsel/kategori/marka) + Stock (adet) bileşimi; ProductId identity + optimistic concurrency.
- Query response'lar (`GetProductStorefrontView`, `GetStorefrontProductList`) indirim alanı taşımaz.

### SupplierProductSnapshot (Supplier.Gateway kanonik + Shared kontrat)

- Kalkan: `DiscountPercent` (kontrat + kanonik + wire) ve `DiscountCode` (Supplier.Api wire + dataset).
- Kalan alanlar ve snapshot-diff (record eşitliği) semantiği değişmez.

## Değişen dış görünümler (özet — ayrıntı contracts/)

- Basket API: kupon endpoint'leri ve MCP tool'ları kalkar; `GetBasket` yanıtı indirim alanları taşımaz.
- Order API: `CreateOrder` girdisinde `DiscountRate` yoktur.
- Storefront API: iki query yanıtından `DiscountRate` düşer.
- Identity: `discount.read`/`discount.write` scope'ları ve `discount.api` resource'u tanımsızdır.