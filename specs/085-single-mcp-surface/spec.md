# Feature Specification: Tek MCP Yüzeyi — /mcp-admin Sökümü + Scope-Bazlı Tool Budaması

**Feature Branch**: `085-single-mcp-surface`

**Created**: 2026-09-26

**Status**: Draft

**Input**: User description: "Tek müşteri+admin MCP ucu: /mcp-admin yüzeyinin sökümü; tool görünürlüğü yol-prefix yerine token scope budamasına geçer; iki OAuth istemci tek uca bağlanır; DCR tavanı değişmez; fasadın platforma taşınması kapsam dışı."

**Kademe**: Tam — endpoint kontratı değişiyor (dış istemcilerin bağlandığı `/mcp-admin` ucu kalkıyor), 5 servis (fasat + catalog/stock/customer/discount) + gateway rotaları + IdP davranışı etkileniyor.

## Clarifications

### Session 2026-09-26

- Q: Tek uçta scope ilanı (PRM keşfi) nasıl çalışacak? → A: Tek PRM, `scopes_supported` = müşteri+admin birleşimi (Option A). Sonuç şartı: tavan-üstü scope talebi bağlantıyı kırmamalı — IdP tavan dışını eler, kısıtlı token basar (FR-006).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin tek uçtan tüm yetkili tool'larını görür (Priority: P1)

Admin, Claude Desktop'taki admin kaydıyla mağazanın TEK `/mcp` ucuna bağlanır ve login olur. Tool listesinde hem müşteri hem admin tool'ları vardır; admin işlemleri (ürün yayınlama, stok düzeltme) bugünkü gibi çalışır.

**Why this priority**: Sökümün amacı bu — admin yüzeyi ayrı uç olmadan yaşayabilmeli; çalışmazsa feature anlamsız.

**Independent Test**: Admin token'ıyla `/mcp` oturumu aç, `tools/list`'te admin tool'larının varlığını ve bir admin yazma tool'unun başarısını doğrula.

**Acceptance Scenarios**:

1. **Given** admin rolündeki kullanıcı admin istemcisiyle login, **When** `/mcp` oturumu açılır, **Then** tool listesinde müşteri + admin tool'ları birlikte görünür.
2. **Given** aynı oturum, **When** bir admin yazma tool'u çağrılır, **Then** işlem başarılıdır ve AdminActionLog izi bugünkü gibi düşer.
3. **Given** sistem, **When** herhangi bir istemci `/mcp-admin`'e bağlanmayı dener, **Then** uç artık yoktur (404).

---

### User Story 2 - Müşteri admin tool'larını hiç görmez (Priority: P1)

Müşteri rolündeki kullanıcı aynı `/mcp` ucuna bağlanır. Tool listesinde yalnız müşteri tool'ları vardır; admin tool'ları ne listede görünür ne şeması sızar. Bir şekilde adıyla çağrılırsa işlem gerçekleşmez, anlaşılır hata döner (403 son savunma — kabul edilmiş UX).

**Why this priority**: Güvenlik sınırı; US1 ile aynı mekanizmanın iki yüzü.

**Independent Test**: Müşteri token'ıyla oturum aç; `tools/list`'te admin tool sayısının 0 olduğunu, adıyla doğrudan çağrının reddedildiğini doğrula.

**Acceptance Scenarios**:

1. **Given** müşteri token'lı oturum, **When** `tools/list` çekilir, **Then** admin tool'ları listede yoktur.
2. **Given** müşteri token'lı oturum, **When** bir admin tool'u adıyla `tools/call` edilir, **Then** işlem gerçekleşmez; kullanıcıya yetki hatası döner.
3. **Given** token'sız oturum (anonim BC ucu), **When** `tools/list` çekilir, **Then** yalnız anonim müşteri seti görünür.

---

### User Story 3 - İPTAL (kullanıcı kararı, 2026-09-26)

Aynı makinede admin + müşteri Desktop kayıtlarını logout'suz yan yana tutma ihtiyacı "fantezi"
bulundu — günlük kullanımda gereksiz karmaşıklık. Normal akış: TEK kayıt, hesap değişimi gerektiğinde
logout+re-login. Ayrıca canlı denemede mcp-remote'un token cache'i CLIENT_ID'ye değil SUNUCU URL'İNE
göre anahtarlandığı bulundu (bkz. memory `mcp-remote-url-based-cache-gotcha`) — aynı URL'e iki kayıt
token paylaşımına yol açabiliyordu. FR-007/SC-005 bu nedenle düşürüldü.

---

### Edge Cases

- Token'ında KISMİ admin scope'u olan kullanıcı (ör. gelecekteki catalog-manager: yalnız `catalog.admin`): listede yalnız o scope'un tool'ları görünmeli — budama scope başına, hep-ya-hiç değil.
- Dış DCR istemcisi admin scope'u talep ederse: verilmez; müşteri demetiyle sınırlı token basılır (bugünkü tavan aynen).
- Oturum açıkken rol/scope değişirse: mevcut oturum eski setiyle sürer; yeni set sonraki oturumda (token yenilenince) — kabul edilir.
- Fasat keşfi (m2m) tek uçtan tüm tool'ları toplayabilmeli; makine token'ının scope'ları admin tool'larının keşfini kapsamalı (keşif ≠ çağrı ayrımı sürer).
- Müşteri istemcisi union PRM'den admin scope'larını da talep ederse: bağlantı kırılmaz; token yalnız istemci tavanı ∩ kullanıcı rolü kesişimiyle basılır (FR-006).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Fasat ve downstream BC'ler (catalog/stock/customer/discount) TÜM tool'larını tek `/mcp` ucundan sunmalı; `/mcp-admin` uçları, PRM'leri ve gateway yönlendirmeleri kaldırılmalı.
- **FR-001a**: Discount BC'nin bugünkü TEK ucu `/mcp-admin`; korumalı `/mcp`'ye taşınır (anonim seti yok — token'sız oturum boş liste görür).
- **FR-002**: Oturum tool seti, oturumu açan token'ın scope'larından kurulmalı: scope yoksa o scope'un tool'ları listeye girmez; budama scope başına çalışır (kısmi admin destekli).
- **FR-003**: Token'sız oturum (anonim BC uçları) yalnız anonim müşteri setini görmeli.
- **FR-004**: Handler'lardaki scope zorlaması (`[RequiredScope]` + endpoint koruması) aynen kalmalı; budama kaçağında çağrı reddedilir (403/tool-error son savunma).
- **FR-005**: İki OAuth istemcisi sürmeli: müşteri istemcisi yalnız müşteri demetini, admin istemcisi admin demetini talep edebilmeli; dış DCR istemcileri admin scope'u alamamalı (tavan değişmez).
- **FR-006**: Scope keşfi TEK PRM'den: `scopes_supported` müşteri+admin birleşimini ilan eder. İstemcinin tavanı DIŞINDAKİ scope talebi bağlantıyı kırmamalı: IdP tavan-dışını eleyip istemcinin izinli demetiyle kısıtlı token basar (reddetmez).
- **FR-007**: İPTAL (kullanıcı kararı 2026-09-26) — bkz. User Story 3.
- **FR-008**: Mevcut müşteri ve admin akışları (arama, sepet, sipariş; ürün/stok/merchant yönetimi + AdminActionLog izi) davranış değiştirmeden sürmeli.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, tek kayıt + tek login ile bugün iki uçta yaptığı işlerin tamamını yapabilir; `/mcp-admin` bilgisi hiçbir istemci config'inde gerekmez.
- **SC-002**: Müşteri oturumlarının tool listesinde admin tool'u sayısı 0; adıyla doğrudan çağrı denemesi %100 reddedilir.
- **SC-003**: Dış DCR istemcisinin admin scope elde etme denemesi %100 başarısız.
- **SC-004**: Mevcut canlı akışlar (müşteri alışverişi uçtan uca + admin ürün yayınlama) regresyonsuz — davranış bugünle birebir.
- **SC-005**: İPTAL (kullanıcı kararı 2026-09-26) — bkz. User Story 3.

## Assumptions

- İstemci modeli (b) kilitli: iki OAuth istemci (`external-customer-agent` + `external-admin-agent`) kalır; birleşen yalnız uç.
- `RequireLoginUpfront=true` modu ve uyuyan step-up kodu bu feature'da değişmez; anonim gezinti açılmıyor.
- Rol→scope RBAC omurgası (İLKE V) ve rol atama yüzeyi (AgentPlatform Identity `Pages/Admin`) değişmez; yeni scope doğmuyor.
- Fasadın AgentPlatform'a taşınması (dilim B) kapsam DIŞI; bu iş taşımadan ÖNCE yapılır.
- İki Desktop kaydının aynı-URL cache ayrışma mekanizması (URL işareti vs statik istemci bilgisi) plan aşamasının kararı; spec yalnız ayrışma ŞARTINI koyar (FR-007).
- Tool adları global benzersiz kalır (`Shared/McpToolNames`); tek uçta ad çakışması yeni risk doğurmaz.
