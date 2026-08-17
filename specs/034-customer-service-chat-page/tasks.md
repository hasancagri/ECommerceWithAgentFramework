# Tasks: Müşteri Hizmetleri Tam-Sayfa Chat Ekranı

**Input**: Design documents from `/specs/034-customer-service-chat-page/`

**Prerequisites**: spec.md (küçük feature — plan.md bilinçli yok, anayasa Artefakt Ölçekleme)

**Tests**: Saf domain mantığı yok (WebApp-only UI) — Domain-TDD kapsamı dışı; doğrulama canlı (Aspire).

**Organization**: Görevler user story bazında; her story bağımsız test edilebilir.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

Yok — mevcut WebApp projesi üzerinde çalışılır, yeni proje/paket gerekmez.

---

## Phase 2: Foundational

**Purpose**: Sayfa iskeleti; US1 ve US3 bu sayfa üzerinde yaşar.

- [X] T001 Yeni Razor Page: src/ui/WebApp/Pages/MusteriHizmetleri.cshtml (+ .cshtml.cs, route /musteri-hizmetleri)
- [X] T002 Sayfa işaretlemesi: tam-yükseklik chat düzeni — mesaj listesi + form (id'ler chat-page-* ile, widget id'leriyle çakışmaz)
- [X] T003 [P] Sayfa stilleri: src/ui/WebApp/wwwroot/css/chat-page.css (tam-yükseklik; kayan mesaj listesi; site görünümüyle uyumlu)

**Checkpoint**: /musteri-hizmetleri boş chat ekranıyla açılıyor.

---

## Phase 3: User Story 1 - Metinle uçtan uca alışveriş (Priority: P1) 🎯 MVP

**Goal**: Login'li kullanıcı geniş ekranda yazışarak ara→sepet→sipariş→ödeme zincirini tamamlar.

**Independent Test**: Login'li kullanıcı sayfada zinciri yalnız yazışarak bitirir.

### Implementation for User Story 1

- [X] T004 [US1] Yeni src/ui/WebApp/wwwroot/js/chat-page.js: SSE akış/parse mantığı chat-widget.js'ten taşınır (fetch /chat/stream, data: satırları, tool/reasoning filtresi, previousResponseId sessionStorage, link-origin düzeltmesi)
- [X] T005 [US1] chat-page.js sayfa davranışı: gönder→user balonu→bot balonuna akış; akış sürerken input disabled (Edge case)
- [X] T006 [US1] Hata yolu: fetch/akış hatasında "Üzgünüm..." mesajı + input yeniden aktif (FR-008)
- [X] T007 [US1] MusteriHizmetleri.cshtml: chat-page.css + chat-page.js referansları; data-authenticated attribute (auth-state reset mantığı korunur)

**Checkpoint**: Sayfada login'li uçtan uca yazışma akıyor.

---

## Phase 4: User Story 2 - Icon'dan sayfaya geçiş (Priority: P2)

**Goal**: Sağ-alt 💬 icon panel yerine /musteri-hizmetleri'ne götürür; açılır panel ölür.

**Independent Test**: Herhangi bir sayfada icon tıkla → sayfa açılır; hiçbir sayfada panel yok.

### Implementation for User Story 2

- [X] T008 [US2] src/ui/WebApp/Pages/Shared/_ChatWidget.cshtml: panel+form kaldırılır; icon `<a href="/musteri-hizmetleri">💬</a>` olur
- [X] T009 [US2] src/ui/WebApp/wwwroot/js/chat-widget.js SİLİNİR (mantık T004'te sayfaya taşındı); _Layout.cshtml'deki script referansı kaldırılır
- [X] T010 [US2] src/ui/WebApp/wwwroot/css/chat-widget.css sadeleşir: panel stilleri silinir, yalnız icon stili kalır
- [X] T011 [US2] MusteriHizmetleri sayfasında icon gizlenir (sayfa kendini işaret etmesin — FR-002/US2 senaryo 2)
- [X] T012 [US2] src/ui/WebApp/Pages/Shared/_Layout.cshtml header'a "Müşteri Hizmetleri" nav linki (FR-010)

**Checkpoint**: Panel tamamen kalktı; giriş noktaları (icon + nav) sayfaya götürüyor.

---

## Phase 5: User Story 3 - Anonim kullanıcı deneyimi (Priority: P3)

**Goal**: Anonim kullanıcı sayfada ürün arar; giriş yapmadığı bilgisi + login linki görür.

**Independent Test**: Logout tarayıcıda sayfa açılır, arama yanıt verir, login linki görünür.

### Implementation for User Story 3

- [X] T013 [US3] MusteriHizmetleri.cshtml: anonim ise bilgi şeridi + mevcut giriş sayfasına link (FR-007); login'liyse gizli

**Checkpoint**: Üç story de bağımsız çalışır durumda.

---

## Phase 6: Polish & Doğrulama

- [ ] T014 Canlı doğrulama (Aspire): login'li uçtan uca zincir — ara→sepete ekle→sipariş→kayıtlı kartla öde (SC-001)
- [ ] T015 Canlı doğrulama: icon yönlendirme her sayfada + panel yokluğu + nav linki (SC-002); anonim akış + login linki (SC-003)
- [ ] T016 Canlı doğrulama: akış kesme/hata senaryosu — ChatAgent durdurup mesaj at, hata mesajı + input aktif (SC-004)
- [X] T017 README/dokümantasyon gerekiyorsa güncelle (chat widget bölümü varsa sayfaya evrildi notu)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: T001→T002; T003 paralel. US'leri bloklar.
- **US1 (Phase 3)**: Phase 2 sonrası. T004→T005→T006→T007 sıralı (aynı dosyalar).
- **US2 (Phase 4)**: T004 sonrası güvenli (mantık taşınmadan widget silinmez). T008-T011 sıralı; T012 [P] olabilirdi ama aynı _Layout dosyası → sıralı.
- **US3 (Phase 5)**: Phase 2 sonrası; US1/US2'den bağımsız.
- **Polish (Phase 6)**: tüm story'ler sonrası.

### Parallel Opportunities

- T003 (css) Phase 2 içinde T001/T002 ile paralel.
- US3 (T013), US1 akış işlerinden bağımsız — Phase 2 biter bitmez paralel girilebilir.

---

## Implementation Strategy

### MVP First (US1)

1. Phase 2 (iskelet) → Phase 3 (US1 akış) → canlı dene.
2. Sonra US2 (widget'ı öldür, yönlendir) — bu sıra önemli: önce yeni yol çalışsın, sonra eski yol kapansın.
3. US3 (anonim şerit) en son; küçük.

### Not

- chat-widget.js'te tek tüketici kalmayacağı için "ortak fonksiyon çıkarma" yerine mantık chat-page.js'e TAŞINIR; widget JS'i tamamen silinir (spec FR-003).
- Backend/BFF/ChatAgent dosyalarına dokunulmaz (FR-009).