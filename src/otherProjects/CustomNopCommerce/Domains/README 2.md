# CustomNopCommerce — Domain modülleri

nopCommerce'in anemik `Nop.Core/Domain` yapısı, bu projede **zengin aggregate + vertical slice +
Result pattern** idiomuna uyarlanarak modül-modül taşınır. Aspire'a bağlı değildir; yalnız ana
repo `Common`'ını paylaşır. MCP yoktur. spec-kit yalnız ileride parça ana ECommerce'e **extract**
edilirken kullanılır.

## Modül durumu

| Modül | Durum | Kapsam |
|-------|-------|--------|
| Catalog-Core | ✅ scaffold | Product, Category, ProductTag |
| Catalog-Variants | ✅ scaffold | ProductAttribute, Mapping, Combination |
| Catalog-Specifications | ✅ scaffold | SpecificationAttribute(+Group), ProductSpecificationAttribute |
| Catalog-Recommendations | ✅ scaffold | ProductRecommendation (Related+CrossSell birleşik) |
| Reviews | ✅ scaffold | ProductReview (rating+moderasyon+faydalı-oy), ReviewType |
| Ordering | ✅ scaffold | Order(+OrderItem+OrderNote), GiftCard, CheckoutAttribute |
| Customers | ✅ scaffold | Customer (profil + adres defteri; auth/rol IdP'de) |
| Pricing | ✅ scaffold | Discount (kupon/kural/limit), TierPrice (PriceList deferred) |
| Shipping | ✅ scaffold | ShippingMethod (rate), Warehouse, Shipment(+item) |
| Tax | ✅ scaffold | TaxCategory, TaxRate (CalculateTax) |
| Directory | ✅ scaffold | Country(+State), Currency (Convert), Measure (birleşik) |
| Loyalty | ✅ scaffold | RewardPointsAccount (ledger, bakiye türetilir) |
| Messaging | ✅ scaffold | MessageTemplate, NewsLetterSubscription, QueuedEmail |
| Vendors | ✅ scaffold | Vendor (+VendorNote; Product.VendorId hedefi) |
| Seo | ✅ scaffold | UrlRecord (slug geçmişi + redirect) |
| Gdpr | ✅ scaffold | GdprConsent, GdprLogEntry (append-only audit) |
| Affiliates | ✅ scaffold | Affiliate (referral slug; Order.AffiliateId hedefi) |

**Roadmap TAMAM — 16 modül, ~40 aggregate scaffold.** Sıradaki adım: bir parçayı `/speckit-specify` ile gerçek ECommerce'e extract.

---

## Catalog-Core — god-entity bölünmesi (öğrenme notu)

nopCommerce `Product` = ~100 alanlık tek anemik sınıf. DDD/mikroservis idiomunda bu **tek aggregate
olamaz** — alanlar farklı bounded context'lere aittir. Bölünme:

### Product aggregate'inde KALAN (katalog kimliği + sunum)
- Kimlik: `Name`, `ShortDescription`, `FullDescription`, `Sku`, `Gtin`, `ManufacturerPartNumber`
- `ProductType` (Simple/Grouped) + `ParentGroupedProductId`
- `Money Price` (liste fiyatı — VO), `ProductDimensions` (fiziksel ölçü — VO)
- `SeoMetadata` (VO), sunum bayrakları: `Published`, `ShowOnHomepage`, `MarkAsNew`, `AllowCustomerReviews`
- Eşlemeler: `Categories` (çok-a-çok, featured+sıra), `TagIds`

### BAŞKA modüle taşınan (Product'ta DURMAZ)
| nopCommerce alanı | Gider → |
|---|---|
| StockQuantity, ManageInventoryMethod, Backorder, MinStock, Warehouse | **Stock / Inventory** |
| IsShipEnabled, IsFreeShipping, AdditionalShippingCharge, DeliveryDate | **Shipping** |
| IsTaxExempt, TaxCategoryId | **Tax** |
| TierPrice, indirim, PriceList | **Pricing** |
| ApprovedRatingSum, ApprovedTotalReviews | **Reviews** (Product'a event ile denormalize) |
| VendorId | **Vendors** |

### ERTELENEN (ayrı ürün-türü modülü, şimdilik yok)
- Rental (kiralama), Download (indirilebilir), GiftCard, Recurring (abonelik)
- ACL / Store mapping (çok-mağaza + güvenlik)

### ATILAN (UI/altyapı — modele girmez)
- ProductTemplate, EditorSettings, Picture/Video/3dObject (medya), PageSize seçenekleri

**Sonuç:** ~100 alan → odaklı bir Catalog aggregate + net modül sınırları. Her alanın "nereye ait
olduğu" e-ticaret domain mantığının özüdür.

---

## Catalog-Variants — 3 aggregate'e bölünme

nopCommerce 5 entity ile variant/attribute'u modeller. Burada 3 aggregate + child'lar:

| Aggregate | nopCommerce karşılığı | Rolü |
|---|---|---|
| `ProductAttribute` | ProductAttribute + PredefinedProductAttributeValue | Global sözlük (Renk/Beden) + değer şablonu |
| `ProductAttributeMapping` | ProductAttributeMapping + ProductAttributeValue | Ürün↔attribute bağı + kontrol tipi + seçilebilir değerler (`AttributeValueOption` child) |
| `ProductAttributeCombination` | ProductAttributeCombination | Satılabilir varyant: SKU + seçilen değerler + ezici fiyat |

**Kilit karar:** nopCommerce Combination'ında `StockQuantity` var — buraya ALINMADI. Varyant stoğu
Stock BC'nin işi (SKU anahtarıyla). `AttributesXml` yerine tipli `SelectedValueIds` (Guid listesi).
Görsel alanları (Picture) File'a, fiyat ayarı (`PriceAdjustment` VO) gerçek uygulaması Pricing'e bırakıldı.

---

## Catalog-Specifications — Variant'tan farkı

**En önemli e-ticaret ayrımı:** Attribute (variant) vs Specification.

| | Attribute / Variant | Specification |
|---|---|---|
| Müşteri seçer mi? | Evet (Renk/Beden) | Hayır (tanımlayıcı) |
| SKU üretir mi? | Evet (satılabilir varyant) | Hayır |
| Fiyat/stok etkiler mi? | Evet | Hayır |
| Amaç | Sipariş kalemi | Facet filtre + ürün sayfası |

3 aggregate: `SpecificationAttributeGroup` (başlık), `SpecificationAttribute` (Ekran Boyutu; `SpecificationAttributeOption`
child = "6.1 inç"), `ProductSpecificationAttribute` (ürüne atama). Invariant (handler): `Option` türü geçerli
seçenek Id ister (spec'e ait olmalı); custom türler serbest değer ister. `AllowFiltering` facet'e girişi kontrol eder.

---

## Catalog-Recommendations — birleştirme (god-split'in TERSİ)

nopCommerce `RelatedProduct` + `CrossSellProduct` = iki neredeyse-aynı anemik tablo (ProductId1→ProductId2).
Burada TEK `ProductRecommendation` aggregate'ine toplandı; ayrım `RecommendationType` enum'ıyla (Related =
ürün sayfası "benzer ürünler" sıralı; CrossSell = sepet "birlikte alınanlar"). Ders: her zaman BÖLMEK değil —
yakın-aynı iki tabloyu anlamlı tek kavrama BİRLEŞTİRMEK de DDD modellemesidir.

Invariant (handler, query gerekir — tek aggregate sınırında görülmez): kendine-bağ yasak; aynı
(kaynak,hedef,tür) tekrarı yasak; iki ürün de var olmalı. Extract'ta bu okuma ChatAgent öneri tool'unu besler.

---

## Reviews — AYRI BC + türetilmiş durum

Reviews kendi bounded context'idir; Catalog'un Product'ına ERİŞMEZ — `ProductId`/`CustomerId` opak Id
(gerçek mikroservis sınırı; monolit içinde bile cross-load yapılmaz).

`ProductReview` zengin aggregate dersleri:
- **Türetilmiş durum:** `HelpfulYesTotal`/`HelpfulNoTotal` AYRI alan değil — faydalı-oy koleksiyonundan
  computed property ile hesaplanır. Sayaç hep oylarla tutarlı (drift imkânsız).
- **Müşteri başına tek oy** invariant'ı `AddHelpfulnessVote`'ta.
- **Moderasyon yaşam döngüsü:** onaysız doğar → `Approve()`; liste yalnız onaylıları verir.
- Puan 1-5 invariant'ı (overall + çok-kriterli `CriteriaRating`).

`ReviewType` = çok-kriterli boyut (Kalite/Fiyat). nop StoreId → çok-mağaza, CustomerNotifiedOfReply → Messaging.

---

## Ordering — en sert god-split (Order ~65 alan)

`Order` = ikinci dev god-entity. Bölünme kararları:

| nopCommerce alanı | Karar |
|---|---|
| CardNumber, CardCvv2, MaskedCreditCardNumber, CardType... | **TAMAMEN SİLİNDİ** (PCI — asla saklanmaz) → PaymentGateway |
| Authorization/Capture TransactionId... | → Payment BC; kalan yalnız `PaymentStatus` |
| ShippingMethod, RateComputation... | → Shipping BC; kalan yalnız `ShippingStatus` |
| Her tutarın InclTax + ExclTax ikizi, TaxRates, VatNumber | → tek tutara indirgendi; vergi Tax BC hesaplar (`OrderTotals`) |
| AffiliateId / RewardPointsHistoryEntryId | → Affiliates / Loyalty |
| StoreId / CustomerLanguageId / Recurring | → deferred |

**Kalan Order:** CustomerId + adres Id'leri (opak) + 3 statü + `OrderTotals` özeti + `OrderItem` kalemler + `OrderNote`.
Dersler: satır/ara toplam TÜRETİLİR (`OrderItem.LineTotal`, `Order.ItemsSubtotal`); iptal invariant'ı (Complete/Cancelled iptal edilemez); ödeme Pending→Processing geçişi.

3 aggregate: **Order**, **GiftCard** (bakiye = başlangıç − Σ kullanım, TÜRETİLİR; aktiflik+bakiye invariant'ı),
**CheckoutAttribute** (sepet geneli seçim; ProductAttribute'a benzer ama Ordering BC — kendi kontrol-tipi enum'ı).
Money = Ordering'in KENDİ Money'si (Catalog'unkini paylaşmaz — BC izolasyonu).

---

## Customers — kimlik ≠ profil ayrımı

nopCommerce `Customer` (~50 alan) kimlik + auth + rol + profil + adres + puanı tek entity'de karıştırır.
En önemli karar: **KİMLİK/AUTH BU BC'YE GİRMEZ.**

| nopCommerce alanı | Karar |
|---|---|
| Username, Password, FailedLoginAttempts, MFA, OTP, ExternalAuth | → **Identity.Server** (OpenIddict + ASP.NET Identity) |
| CustomerRole, RoleMapping | → **RBAC/Identity** (rol = scope demeti, 030); bu BC rol görmez |
| RewardPointsHistory | → **Loyalty** modülü |
| VatNumberStatus, Affiliate, Vendor, timezone, follow-up, LastIp | → deferred / drop |

**Kalan Customer:** profil (ad/soyad/cinsiyet/DOB/şirket/telefon/VAT) + **adres defteri**. Adres child entity
(Id'li — sipariş + varsayılan Id'si referanslar). Dersler: varsayılan fatura/teslimat adresi mutlaka defterde
olmalı (invariant); ilk adres otomatik varsayılan; adres silinince varsayılan sıfırlanır. `CountryId` opak (Directory BC).

---

## Pricing — davranış-zengin aggregate (hesap domain'de)

`Discount` = zengin aggregate'in en iyi örneği: **hesaplama domain'de, handler'da değil.**
- `CalculateDiscount(baseAmount)` — saf metot: yüzde/sabit + üst sınır + taban-aşımı guard'ı.
- `IsValidAt(now, coupon)` — saf sorgu: aktiflik + tarih penceresi + kupon eşleşmesi.
- `RecordUsage(...)` — kullanım limiti invariant'ı (NTimesOnly / NTimesPerCustomer, `DiscountUsage` child'lardan sayılır).
- `ValidateCoupon` query bu saf metotları çağırır (durum değiştirmeden hesap verir).

`TierPrice` = adet-kırılımı fiyat (10+ → 45 TL); `AppliesTo(qty, now)` saf kontrol. ProductId/CustomerRoleId opak.
Pricing BC **saf decimal** kullanır (Money VO değil — tüketen BC sonucu kendi Money'sine sarar).
PriceList (B2B toplu liste) ertelendi. Aktiflik için AggregateRoot.IsActive yeniden kullanıldı.

---

## Shipping — net yeni BC (sende yoktu)

3 aggregate: **ShippingMethod** (ücret kuralı domain'de: `CalculateRate(subtotal)` → eşik geçilirse ücretsiz),
**Warehouse** (adres opak Id), **Shipment** (+`ShipmentItem` child).

`Shipment` = **sevk yaşam döngüsü invariant'ı** dersi: takip numarası olmadan kargolanamaz; kargolanmadan
teslim edilemez (sıra tarih damgalarıyla korunur — `MarkAsShipped` guard tracking, `MarkAsDelivered` guard shipped).
OrderId/OrderItemId/WarehouseId opak referans. nopCommerce'te ücret plugin'de; burada öğrenme için basit domain kuralı.
