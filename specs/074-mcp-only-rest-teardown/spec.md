# Feature Specification: MCP-Only Yüzey — Domain REST Söküm + Catalog Admin Parite

**Feature Branch**: `074-mcp-only-rest-teardown`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "Domain/iş REST endpoint'lerini söküp mağazayı tam MCP-only yüzeye taşı; catalog admin parite tool'larını kur."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin agent catalog'u tam MCP üzerinden yönetir (Priority: P1)

Bir mağaza yöneticisi kendi AI istemcisiyle `/mcp-admin` yüzeyine bağlanır ve katalog
bakımını (kitap künyesi oluşturma, kategori/yazar/etiket/özellik yönetimi, ölçü ve SEO
düzenleme) tamamen MCP tool'ları ile yapar. Hiçbir REST ekranı veya HTTP çağrısı gerekmez.

**Why this priority**: Bu feature'ın kalbi. REST admin yüzeyi WebApp'le birlikte söküldü;
o işlevler bugün hiçbir yüzeyden erişilemez. Parite tool'ları olmadan katalog bakımı
imkansız — bu hikaye canlıyken mağaza fiilen yönetilebilir olur.

**Independent Test**: `/mcp-admin` (catalog) oturumu aç; sırayla category oluştur, author
oluştur, product-tag oluştur + ürüne ata, specification-attribute + option ekle,
create_product ile künye gir, dimensions ve SEO ata. Her çağrı başarılı döner ve
`AdminActionLog`'a iz düşer.

**Acceptance Scenarios**:

1. **Given** admin `/mcp-admin` oturumu açık, **When** `create_product` gerekli künye
   alanlarıyla çağrılır, **Then** ürün oluşur, `ProductLinked` yayınlanır (Stock + Storefront
   akışı tetiklenir) ve `AdminActionLog` kaydı düşer.
2. **Given** var olan bir ürün, **When** admin `set_product_dimensions` / `set_product_seo`
   çağırır, **Then** alanlar güncellenir ve iz kaydı oluşur.
3. **Given** yeni kategori/yazar/etiket, **When** create/update/rename tool'ları çağrılır,
   **Then** kayıt işlenir; aynı tool `/mcp` (anonim müşteri) ucunda GÖRÜNMEZ.
4. **Given** yazma-scope'u OLMAYAN token, **When** admin tool çağrılır, **Then** 403 döner.

---

### User Story 2 - Domain/iş REST yüzeyi tümüyle kalkar (Priority: P1)

Bir geliştirici çözümü (otherProjects hariç) tarar ve catalog/stock/customer-merchant/checkout
altında hiçbir domain iş REST endpoint'i bulamaz. İş yüzeyinin tek kapısı MCP'dir. Yetim kalan
Command/Query slice'ları, `.http` dosyaları ve gateway'in yetim REST proxy rotası da temizlenmiştir.

**Why this priority**: "Sadece MCP olsun" isteğinin doğrudan karşılığı. Ölü/yetim REST yüzeyi
saldırı yüzeyi + bakım yükü; anayasa (BC izolasyonu, agent-yüzey) ile hizalanır.

**Independent Test**: Kod tabanında domain iş endpoint'i (Map{Get,Post,Put,Delete} — S2S/auth/
MCP-infra hariç) arayışı boş döner; `.http` dosyaları yoktur; gateway config'inde `catalog-route`
kalmaz. Sistem Aspire'dan sorunsuz açılır.

**Acceptance Scenarios**:

1. **Given** söküm tamam, **When** catalog/stock/customer/checkout projeleri taranır,
   **Then** yalnız KORUNAN uçlar (auth, MCP-infra, S2S internal, gRPC) kalır.
2. **Given** kullanıcı/admin yüzeyli Command/Query slice'ları, **When** söküm yürür,
   **Then** Agent ikizi olan REST'iyle silinir, olmayan için önce Agent ikizi kurulur;
   son durumda `Features/Commands|Queries`'te yalnız kritik/iç (S2S/saga/import) slice kalır.
3. **Given** temizlik tamam, **When** `dotnet build` çalışır, **Then** çözüm hatasız derlenir.

---

### User Story 3 - Müşteri + servis akışları bozulmadan sürer (Priority: P1)

Bir müşteri kendi AI istemcisiyle mağazaya bağlanır: keşif, sepet, checkout uçtan uca çalışır.
Login (OIDC) akışı, MCP OAuth keşfi (PRM), servis-arası ödeme bağlamı çekimi ve checkout gRPC
adımları söküm sonrası da ayaktadır.

**Why this priority**: Söküm hiçbir canlı yolu kırmamalı. Auth + MCP-infra + S2S/gRPC korunan
sınırdır; regresyon en yüksek risk.

**Independent Test**: Chat E2E — keşif (`query_storefront`) → sepete ekle → checkout →
sipariş tamamlanır. Ayrı olarak order charge yolu customer `/internal/payment-context` +
`/internal/merchant-key`'i çekebilir; checkout gRPC sepet temizler.

**Acceptance Scenarios**:

1. **Given** müşteri MCP oturumu, **When** keşif→sepet→checkout akışı yürütülür, **Then**
   sipariş `Confirmed` olur (canlı PASS).
2. **Given** checkout sağası çalışır, **When** charge adımı ödeme bağlamı ister, **Then**
   S2S `/internal/payment-context` yanıt verir (REST söküm bunu etkilememiştir).
3. **Given** MCP istemcisi bağlanır, **When** OAuth keşfi yapılır, **Then** PRM (RFC 9728)
   metadata uçları yanıt verir.

---

### Edge Cases

- Bir Command/Query slice'ı hem REST hem başka bir iç tüketici tarafından mı kullanılıyor?
  → Söküm öncesi çağıran taraması; başka canlı çağıran varsa slice KALIR (yalnız endpoint kalkar).
- `create_product` doktrin kayması: import (051) ile aynı üründe (ProductId=ISBN) çakışma —
  MCP create var olan ISBN'e çarparsa hata/idempotent davranış tanımlı olmalı.
- Gateway'de `catalog-route` kalkınca ClientCredential politikası / `IdentityOption` yalnız o
  rota için miydi? Başka kullanan varsa dokunulmaz.
- Anonim `/mcp` keşif seti yeni admin tool'larla KİRLENMEMELİ (yol-prefix filtresi doğru budamalı).

## Requirements *(mandatory)*

### Functional Requirements

#### Söküm (REST kaldırma)

- **FR-001**: Sistem, catalog servisindeki tüm domain iş REST endpoint'lerini (products,
  categories, authors, publishers, product-tags, specification-attributes — admin yazma +
  public read) kaldırMALI.
- **FR-002**: Sistem, stock servisindeki tüm REST endpoint'lerini (set/increase/decrease/
  get-by-id/get-all) kaldırMALI.
- **FR-003**: Sistem, customer servisindeki merchant-information admin REST uçlarını
  (GET/POST) kaldırMALI; `/internal/payment-context` ve `/internal/merchant-key` uçlarına
  DOKUNMAMALI.
- **FR-004**: Sistem, checkout servisindeki POST `/checkout` REST endpoint'ini kaldırMALI
  (checkout broker `StartCheckout` ile tetiklenir).
- **FR-005**: Son-durum ilkesi — `Features/Agents/*` müşteri+admin iş yüzeyinin TEK yeri olMALI;
  `Features/Commands` ve `Features/Queries`'te YALNIZ kritik/iç (S2S/saga/import) slice'lar kalMALI.
  Kullanıcı/admin yüzeyli her Command/Query: (a) Agent ikizi VARSA REST'iyle birlikte silinir;
  (b) Agent ikizi YOKSA önce Agent ikizi kurulur (bkz FR-009..FR-015), sonra eski slice silinir.
- **FR-005a**: Sistem, aşağıdaki KRİTİK/İÇ slice'lara DOKUNMAMALI (kullanıcı isteği değil):
  basket `ClearBasketByCheckout` (checkout saga) + `GetBasket` (gRPC basket_items → order charge);
  stock `CommitStock` + `RevertCommitStock` (checkout saga); customer `GetMerchantKeyInternal` (S2S);
  catalog `ImportBook` (051 import yolu).
- **FR-006**: Sistem, 5 `.http` dosyasını (order/basket/payment/catalog/customer) silMELİ.
- **FR-007**: Sistem, gateway config'indeki yetim `catalog-route` REST proxy rotasını ve
  yalnız o rota için var olan ClientCredential/IdentityOption ayarlarını kaldırMALI.
- **FR-008**: Sistem, kullanılmaz kalan `*EndpointExtension` iskeletlerini temizleMELİ
  (map çağrısı kalmayanlar).

#### Kurma (Catalog admin parite tool'ları — yalnız `/mcp-admin`)

- **FR-009**: Sistem, admin `/mcp-admin` ucunda `create_product` tool'u sağlaMALI (elle
  kitap künyesi oluşturma); doktrin import-only'den admin-yazma-dahil'e genişler.
- **FR-010**: Sistem, category create + update tool'ları sağlaMALI.
- **FR-011**: Sistem, author create tool'u sağlaMALI.
- **FR-012**: Sistem, product-tag create + rename + ürüne-ata + üründen-çıkar tool'ları
  sağlaMALI.
- **FR-013**: Sistem, specification-attribute create + add-option tool'ları sağlaMALI.
- **FR-014**: Sistem, product dimensions set ve product SEO set tool'ları sağlaMALI.
- **FR-015**: Sistem, düzenleme bağlamı için gereken admin list tool'larını
  (specification-attributes list, product-tags list) sağlaMALI.
- **FR-016**: Tüm yeni admin tool'lar YALNIZ korumalı `/mcp-admin` ucunda görünMELİ; anonim
  `/mcp` keşif seti DEĞİŞMEMELİ (070 yol-prefix filtresi deseni).
- **FR-017**: Her yeni admin yazma tool'u ilgili yazma scope'u ile korunMALI ve salt-append
  `AdminActionLog`'a iz düşMELİ.
- **FR-018**: Her yeni MCP tool ince sarmalayıcı olMALI: yalnız kendi `Features/Agents/
  <X>ForAgent` slice'ını çağırMALI (imperatif çapraz-slice çağrı yok).

#### Koruma (regresyon önleme)

- **FR-019**: Identity.Server OIDC/OAuth/consent endpoint'leri değişmeden çalışMALI.
- **FR-020**: Mcp.Gateway MapMcp + PRM keşfi ve Common `McpResourceMetadataExtension`
  değişmeden çalışMALI.
- **FR-021**: Checkout gRPC (basket_items/basket_clear) ayakta kalMALI.
- **FR-022**: Söküm sonrası çözüm hatasız derlenMELİ ve sistem Aspire AppHost'tan açılMALI.

### Key Entities *(include if feature involves data)*

- **AdminActionLog**: Her admin yazma işleminin salt-append izi (kim, ne, ne zaman); yeni
  parite tool'ları da bu ize yazar.
- **Product (Catalog aggregate)**: Yeni `create_product` ile ISBN (ProductId) tabanlı
  oluşturma; import (051) ile aynı kimlik uzayı.
- **Category / Author / ProductTag / SpecificationAttribute**: Parite tool'larının
  yönettiği catalog yardımcı aggregate/entity'leri.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Çözümde (otherProjects hariç) auth + MCP-infra + S2S/gRPC dışında sıfır domain
  iş REST endpoint'i kalır (tarama ile doğrulanır).
- **SC-002**: WebApp söküm sonrası erişilemez olan tüm catalog admin işlevleri (%100)
  `/mcp-admin` tool'ları ile yeniden erişilebilir.
- **SC-003**: Chat E2E (keşif→sepet→checkout) canlı akışı PASS; hiçbir müşteri yolu kırılmaz.
- **SC-004**: Admin `/mcp-admin` parite akışı (create_product + category/author/tag/spec +
  dimensions/seo) uçtan uca PASS; her yazma `AdminActionLog`'ta izli.
- **SC-005**: Anonim `/mcp` keşif tool seti söküm+kurma öncesiyle aynı kalır (yeni admin
  tool'lar sızmaz).
- **SC-006**: `.http` dosyası sayısı 0; gateway config'inde REST proxy rotası 0.
- **SC-007**: Söküm sonrası `Features/Commands|Queries` altında kalan her slice kritik/iç
  (S2S/saga/import) — kullanıcı/admin yüzeyli hiçbir Command/Query kalmaz (yüzey tümüyle
  `Features/Agents`).

## Assumptions

- Hedef domain REST endpoint'lerinin canlı istemcisi yok (WebApp söküldü); tarama bunu doğrular.
  Başka canlı çağıran bulunursa ilgili slice korunur, yalnız endpoint kalkar.
- `create_product` kitap künyesi girişini import (051) ile aynı kimlik uzayında (ProductId=ISBN)
  yapar; var olan ISBN'e çarpma davranışı plan'da netleşir (hata veya idempotent).
- Stock ve customer-merchant için MCP paritesi zaten mevcut (070); yalnız catalog'ta boşluk var.
- Yeni admin tool'lar mevcut yazma scope'larını (catalog.write vb.) kullanır; yeni scope
  gerekmez (netlik plan aşamasında).
- Söküm + kurma tek feature/PR olarak ilerler; domain süreci değişen BC'lerde FLOW.md aynı
  PR'da güncellenir (İLKE VII).