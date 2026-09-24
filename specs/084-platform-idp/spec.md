# Feature Specification: Platform IdP Terfisi (Dilim A)

**Feature Branch**: `084-platform-idp`
**Created**: 2026-09-23
**Status**: Draft
**Input**: Identity.Server'ı tek uygulamanın login'i olmaktan çıkarıp uygulama-nötr **platform kimlik makamına** terfi ettir; ECommerce'in içinden ÇIKAR, ECommerce ona relying party olarak baksın. Merkezi çok-uygulama MCP programının (bkz. memory `central-multi-app-mcp-direction`) temel taşı. PG'yi bağlama (Dilim B) ve merkezi MCP fasat (Dilim C) BU SPEC'İN DIŞINDA.

## Bağlam

Bugün `identity-server` (OpenIddict + ASP.NET Identity, RBAC, DCR, consent) ECommerce'in içinde ve kavramsal olarak "ECommerce'in login'i". Vizyon: kaç uygulama olursa olsun (ECommerce, PaymentGateway, gelecek) **tek kimlik makamı** — tek hesap, tek login, tek consent. Bu dilim o makamı ECommerce'den çıkarıp uygulama-nötr platform bileşeni yapar; ECommerce ilk relying party olur. Canlı kullanıcı yok — hedef doğru son durum, göç değil.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kimlik makamı ECommerce'in dışında, nötr durur (Priority: P1)

Kimlik makamı artık "ECommerce'e ait" değil; ECommerce'in dışında bağımsız platform bileşeni. Uygulamalar ona **açık yapılandırmayla kayıtlı relying party**'lerdir; ECommerce ilk kayıtlı uygulamadır (gömülü/örtük değil).

**Why this priority**: Nötrlük + dışarı çıkma olmadan ikinci uygulama (Dilim B/C) bu makama güvenemez. Bu dilimin asıl değeri budur.

**Independent Test**: Makam ECommerce'den ayrı çalışır; ECommerce kaynak kodu değiştirilmeden, yalnız yapılandırmayla ikinci bir örnek uygulama ad-uzayı tanıtılabilir.

**Acceptance Scenarios**:
1. **Given** platform kimlik makamı, **When** uygulama kayıt yapılandırması incelenir, **Then** ECommerce açık bir kayıtlı relying party olarak görünür (makama gömülü değil).
2. **Given** yeni bir uygulama ad-uzayı (`<app>.*`) yapılandırılır, **When** makam açılır, **Then** ECommerce kodu değişmeden bu ad-uzayı tanınır.
3. **Given** iki uygulama aynı scope adını (`x.write`) ister, **When** makam açılır, **Then** çakışma reddedilir.

---

### User Story 2 - Login/consent/RBAC çalışır (Priority: P1)

Ayşe (müşteri), operatör ve dış AI istemcisi (DCR akışı) makama login olur, consent görür, korumalı yüzeylere erişir. Downstream servisler token'ı doğrular; yalnız scope'a bakar (rol adına değil).

**Why this priority**: Makam çıkarıldıktan sonra kimlik doğrulama fiilen çalışmalı; yoksa hiçbir uygulama açılmaz.

**Independent Test**: ECommerce'e login → token al → korumalı MCP/uç çalışır; consent akışı çalışır; scope zorlaması doğru davranır.

**Acceptance Scenarios**:
1. **Given** kayıtlı kullanıcı, **When** login olur, **Then** geçerli token alır ve korumalı yüzey açılır.
2. **Given** downstream servis, **When** token doğrular, **Then** yalnız scope'a bakar (rol adına değil) — RBAC 030 kuralı korunur.
3. **Given** dış AI istemcisi, **When** DCR kaydı + consent yapar, **Then** akış çalışır.

---

### Edge Cases
- İki uygulama aynı scope adını isterse → ad-uzayı prefix'i zorunlu; çakışma açılışta reddedilir.
- Mobil/loopback-only bir yol makamı köşeye sıkıştırır mı → yeni loopback-only sert bağımlılık EKLENMEZ (mobil kuzey-yıldızı için kapı açık).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: Kimlik makamı ECommerce'in dışında bağımsız platform bileşeni olarak konumlanmalı; hiçbir tek uygulamaya kavramsal olarak "ait" olmamalı.
- **FR-002**: Uygulamalar makama **açık yapılandırmayla kayıtlı relying party** olmalı; ECommerce ilk kayıtlı uygulamadır (gömülü/örtük değil).
- **FR-003**: Scope'lar uygulama ad-uzayıyla ayrışmalı (`<app>.<yetki>`); makam tek uygulamanın scope setini varsaymamalı; ad-uzayı çakışması açılışta reddedilmeli.
- **FR-004**: Downstream servisler token doğrulamada yalnız scope'a bakmalı (rol adına değil) — RBAC 030 kuralı korunur.
- **FR-005**: Login, consent, DCR ve RBAC scope zorlaması makam çıkarıldıktan sonra çalışır durumda olmalı.
- **FR-006**: Yeni **loopback-only sert bağımlılık** eklenmemeli; makam ileride public-erişilebilir dağıtımı destekleyecek şekilde konumlanmalı (mobil/prod inşası bu dilimde YOK, sadece kapı kapatılmamalı).

### Key Entities
- **Kayıtlı Uygulama (Relying Party)**: kimlik makamına güvenen uygulama; kimliği + sahip olduğu scope ad-uzayı. ECommerce ilk örnek.
- **Scope Ad-uzayı**: `<app>.<yetki>` biçiminde yetki atomu; uygulamaya göre gruplanır.

## Success Criteria *(mandatory)*

- **SC-001**: Kimlik makamı ECommerce'ten ayrı, bağımsız bileşen olarak çalışır.
- **SC-002**: ECommerce kaynak kodu değiştirilmeden, yalnız yapılandırmayla ikinci bir örnek uygulama ad-uzayı makama tanıtılabilir.
- **SC-003**: Login/consent/DCR/korumalı-erişim senaryolarının %100'ü çalışır durumdadır.
- **SC-004**: Aynı scope adını iki uygulamanın istediği çakışma denemesi %100 açılışta reddedilir.

## Assumptions
- Canlı kullanıcı/prod verisi yok; hedef doğru son durum, veri göçü değil. "Bire bir eski davranış" bir kısıt DEĞİL — doğru çalışması yeter.
- MerchantKey gibi S2S makine sırları kimlik makamının dışında kalır (kullanıcı auth'u değil) — bu dilim kapsamında değil.
- IdP çıkışı kilitli yön olan "tek platform repo (IdP + fasat)" hedefine gider; fasat (Dilim C) sonra aynı repoya eklenir.