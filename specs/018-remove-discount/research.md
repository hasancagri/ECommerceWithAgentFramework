# Research: Discount'ın Sistemden Tamamen Kaldırılması

Kod taramasıyla tüm dokunma noktaları çıkarıldı; belirsizlik kalmadı. Kararlar aşağıda.

## K1 — Kaldırma sırası: dıştan içe

- **Decision**: Tüketicilerden servise doğru sökülür: (1) UI/agent izleri, (2) Basket/Order/Storefront
  domain'leri, (3) Shared kontratlar + Identity, (4) AppHost/gateway/slnx + proje silme.
- **Rationale**: Her ara adımda çözüm derlenebilir kalır; kontrat silinince ona bağımlı kod zaten kalmamış olur.
- **Alternatives considered**: Önce projeyi silip derleme hatalarını kovalamak — hata listesi rehber olur ama
  ara durumlar derlenemez; adım adım doğrulama (build/test) kaybolur. Reddedildi.

## K2 — Basket kupon akışı tamamen silinir

- **Decision**: `ApplyDiscountCoupon`/`RemoveDiscountCoupon` slice'ları (Commands+Agent), MCP tool'ları,
  endpoint'leri, `Discount` VO'su ve `BasketItem.PriceByApplyDiscountRate` silinir. `GetTotalPrice` tek fiyattan kalır.
- **Rationale**: Kupon oranı yalnız Discount servisinden çözülüyor (WebApp `IDiscountRefitService.GetDiscountByCoupon`);
  servis yokken akış ölü koddur. Spec varsayımı da bu yönde.
- **Alternatives considered**: Kupon altyapısını "ileride lazım olur" diye bırakmak — ölü UI + ölü slice; sadeleştirme
  hedefiyle çelişir. Reddedildi.

## K3 — Snapshot kontratından `DiscountPercent` alanı çıkarılır

- **Decision**: `SupplierProductSnapshotReceived.DiscountPercent`, `SupplierFeedAdapter` (wire + kanonik) ve
  Supplier.Api feed kontratı + `products.json`'dan indirim alanları silinir.
- **Rationale**: Alanın tek tüketicisi DiscountWrite adımıydı; adım kalkınca alan anlamsız. Maket bizim kontrolümüzde.
- **Risk — republish**: Kanonik snapshot record'undan alan düşünce eski kayıtlı snapshot'lar yeni tipe deserialize
  edilir (Newtonsoft fazla alanı yok sayar); record eşitliği kalan alanlar üzerinden çalışır → toplu "değişmiş"
  algısı beklenmez. Dev DB'si gerekirse sıfırlanabilir (spec varsayımı).

## K4 — Ingestion zinciri 4 adıma iner

- **Decision**: Workflow `Brand → Category → Catalog → Stock → Finish` olur; `05_DiscountWrite` klasörü,
  `DiscountWriterAgent` kaydı, `DiscountWriterResult`, ConstValues talimat/araç sabitleri silinir.
  `SupplierSnapshotHandler`'da StockWrite'ın success-edge'i doğrudan `finish`'e bağlanır.
- **Rationale**: Adımlar arası kimlik taşıma (ProductId) StockWrite'a kadar aynı kalır; Discount zincirin son
  halkasıydı, sökülmesi topolojiyi bozmaz.
- **Alternatives considered**: Adımı no-op bırakmak — LLM çağrısı + MCP bağlantısı boşa çalışır. Reddedildi.

## K5 — Mesajlaşma temizliği ve eski kuyruk/exchange kalıntıları

- **Decision**: `DiscountChangedEvent`, `RabbitMqConstants.DiscountChanged` ve `OrderCreated.Queues.Discount`
  silinir. Storefront'un tek kuyruğu (`storefront.events`) kalır; yalnız `DiscountChangedEvent` handler'ı düşer.
- **Rationale**: Exchange'i yalnız Discount.Api declare edip publish ediyordu; başka üretici yok.
  `discount.order-created` kuyruğunu Discount zaten dinlemiyordu (kodda not var) — sabit ölü.
- **Broker kalıntısı**: Var olan `discount.changed` exchange'i RabbitMQ'da tanımlı kalabilir; üreticisi/tüketicisi
  olmayan boş fanout zararsızdır. Dev ortamında elle silinebilir; otomasyon kapsam dışı (spec varsayımı).
- **Uçuştaki mesaj**: Kuyrukta kalmış eski `DiscountChangedEvent` tüketicisiz kalır; Wolverine bilinmeyen mesaj
  tipini hata loglayıp düşürür, sistem çökmez (edge case spec'te).

## K6 — Identity ve scope temizliği

- **Decision**: `discount.read/write` ApiScope'ları, `discount.api` ApiResource'u, client scope talepleri
  (Config.cs 115, 151), `AuthorizationScopes.DiscountRead/Write`, WebApp OIDC scope ekleri (Program.cs) ve
  `TokenService` scope dizesindeki `discount.read` silinir.
- **Rationale**: Scope'un tek zorlayıcısı Discount.Api'ydi; tanım + talep birlikte kalkmalı ki token istekleri
  geçersiz scope'a düşmesin (Duende bilinmeyen scope talebini reddeder → login kırılır).
- **Sıra notu**: Identity tanımı ile WebApp/ChatAgent talepleri AYNI değişiklik setinde kalkmalı (aksi halde
  çalışan sistemde login bozulur).

## K7 — Storefront ve Order sadeleşmesi

- **Decision**: `StorefrontView.DiscountRate` + `ApplyDiscount` + handler + iki query response'taki alanlar
  silinir; WebApp ürün kartındaki indirim rozeti/üstü çizili fiyat kalkar. `Order.DiscountRate`,
  `CreateOrderCommand.DiscountRate` ve WebApp sipariş akışındaki taşıma silinir.
- **Rationale**: Alanların tek kaynağı Discount'tu; kaynak yokken alan hep null kalır — ölü veri.
- **Alternatives considered**: Alanları nullable bırakıp UI'ı gizlemek — SC-001 ("discount araması 0 sonuç")
  ile çelişir. Reddedildi.

## K8 — Anayasadaki Discount örneği

- **Decision**: Anayasa İlke I'de "Discount" açıklayıcı örnektir; ilke içeriği değişmediği için amendment zorunlu
  değildir. İmplementasyonda örneği yaşayan bir kavramla (ör. "Stock") değiştiren PATCH önerilir.
- **Rationale**: Governance: örnek güncelleme açıklama/düzeltme sınıfıdır (PATCH); ilke ekleme/çıkarma yoktur.