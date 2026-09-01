# Tasks: Son Gezdiklerim (Cihaz-Yerel Şerit)

**Input**: `specs/055-recently-viewed/spec.md` (kademe Küçük — plan yok; uygulama şekli task
notlarında)

**Tests**: Saf domain birimi yok (tamamen UI/istemci) — İlke VI tetiklenmez; doğrulama canlı
(T005). **Not:** setup-tasks.sh plan.md istediğinden bu dosya elle üretildi (Küçük kademe kuralı).

**Uygulama şekli (karar):** Sinyal tarayıcı localStorage'ında (`recentlyViewed` = productId
dizisi, en yeni başta, ≤10). Ana sayfa şeridi BFF üzerinden dolar: küçük istemci script'i id'leri
okuyup WebApp page-handler'ına gönderir, handler kart verisini sunucu tarafında vitrinden çekip
`_ProductCard` ile partial HTML döner (tarayıcı→gateway CORS açılmaz; yeni API kontratı yok).

## Phase 1: Setup

- [X] T001 Branch: `055-recently-viewed` (054 üstüne stacked — 054 henüz merge değil)

## Phase 2: User Story 1 — Gezilen kitaplar ana sayfada şerit olur (P1) 🎯 MVP

- [X] T002 [US1] `src/ui/WebApp/Pages/Products/Detail.cshtml` — detay ziyaretinde localStorage
      güncelleyen küçük inline script: ekle-en-başa, varsa öne taşı, 10'da kes; bozuk JSON'da
      listeyi sıfırla (FR-001/002/009)
- [X] T003 [US1] `src/ui/WebApp/Pages/Index.cshtml.cs` — `OnGetRecentlyViewedAsync(string ids)`
      page-handler: id sırasını KORUYARAK her id için vitrin kartını çek (mevcut
      `StorefrontService.GetProductAsync`), bulunamayan/satış-dışı sessizce atla (FR-007), geçerli
      kart yoksa boş içerik dön; partial render
- [X] T004 [US1] `src/ui/WebApp/Pages/Shared/_RecentlyViewedStrip.cshtml` — "Son Gezdiklerim"
      başlığı + `_ProductCard` kartları; `src/ui/WebApp/Pages/Index.cshtml` — feed/boş-durum
      ALTINA placeholder div + localStorage'ı okuyup handler'ı çağıran, sonucu enjekte eden script
      (liste boşsa istek de başlık da YOK — FR-003/004)

**Checkpoint**: Temiz tarayıcıda 3 ürün gez → ana sayfada sıralı şerit; tekrar ziyaret öne taşır.

## Phase 3: User Story 2+3 — Cihaz-yerel davranış + dayanıklılık (P2/P3)

- [X] T005 [US2] Canlı doğrulama (Aspire + Playwright/elle):
      (a) anonim pencerede gez → şerit login'siz görünür; ayrı profil → liste bağımsız (SC-002);
      (b) sunucuda gezinme kaydı yok (DB kontrol);
      (c) listeye sahte/satış-dışı id ekle → sessiz atlama, hepsi geçersizse şerit yok (SC-003);
      (d) 054 regresyonu: kişisel feed + boş durum + kaldırılan vitrin öğeleri aynen (SC-004);
      (e) satın alınan ürün şeritte kalıyor (FR-008)

## Phase 4: Polish

- [X] T006 `dotnet build` temiz; memory/not güncelle (FLOW.md TETİKLENMEZ — domain süreci
      değişmiyor, WebApp UI işi; CLAUDE.md BC haritası değişmez — backend yok)

## Dependencies

- T001 → hepsi. T002 ‖ (T003→T004). T005 tümünün ardından; T006 en son.
- MVP = T001–T004 (+T005a ile doğrulama).