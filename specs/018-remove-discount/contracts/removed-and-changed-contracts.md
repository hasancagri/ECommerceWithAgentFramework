# Contracts: Silinen ve Değişen Sözleşmeler (018)

Bu feature yeni kontrat eklemez; mevcutları siler/küçültür. Referans: `src/others/Shared/*`.

## Silinen HTTP yüzeyi (Discount.Api ile birlikte)

- `GET  /api/v1/discounts` (GetAllProductDiscounts)
- `GET  /api/v1/discounts/{productId}` / kuponla sorgu (GetDiscountByProductId / by-coupon)
- `PUT/POST /api/v1/discounts` (SetProductDiscount), `DELETE ...` (RemoveProductDiscount)
- MCP: `get_discount`, `set_product_discount`, `remove_product_discount`
- Gateway route'ları: `discount-route`, `discount-mcp-route`, `discount.cluster`

## Silinen Basket yüzeyi

- `PUT    /api/v1/baskets/apply-discount-coupon`
- `DELETE /api/v1/baskets/remove-discount-coupon`
- MCP: `apply_discount_coupon`, `remove_discount_coupon`

## Değişen Basket yanıtı — GetBasket

Kalkan alanlar: `DiscountRate`, `Coupon`, `TotalPriceWithAppliedDiscount`,
satırlarda `PriceByApplyDiscountRate`. `TotalPrice` tek toplam olur.

## Değişen Order girdisi — CreateOrder

`DiscountRate` alanı kalkar; kalan alanlar (adres, ödeme, satırlar) değişmez.

## Değişen Storefront yanıtları

`GetProductStorefrontView` ve `GetStorefrontProductList` yanıtlarından `DiscountRate` kalkar.

## Integration event'leri (Shared.IntegrationEvents)

- SİLİNİR: `DiscountChangedEvent(Guid ProductId, decimal? Rate)`
- DEĞİŞİR: `SupplierProductSnapshotReceived` — `decimal? DiscountPercent` alanı kalkar; diğer alanlar aynı.

## RabbitMQ sabitleri (RabbitMqConstants)

- SİLİNİR: `DiscountChanged` (exchange `discount.changed` + Storefront binding'i)
- SİLİNİR: `OrderCreated.Queues.Discount` (`discount.order-created` — zaten dinleyeni yoktu)

## Supplier feed kontratı (Supplier.Api → Supplier.Gateway)

Wire modelden `DiscountCode` ve `DiscountPercent` kalkar; `products.json` dataset'i hizalanır.

## Identity / scope sözleşmesi

- SİLİNİR: ApiScope `discount.read`, `discount.write`; ApiResource `discount.api`
- SİLİNİR: client scope talepleri (WebApp OIDC, ChatAgent/M2M config, `TokenService` scope dizesi)
- SİLİNİR: `AuthorizationScopes.DiscountRead/DiscountWrite` sabitleri