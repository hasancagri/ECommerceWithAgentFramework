# Research: Product Sale Readiness (Completeness Gating)

Faz 0 — spec'te plana ertelenen NASIL kararlarının çözümü. Tüm kararlar Catalog bounded
context içinde kalır ve anayasaya uyar.

## Decision 1: Tamlık durumu kalıcı alan mı, uçuşta hesaplama mı?

**Decision**: `Product` aggregate'ine kalıcı bir `bool IsComplete { get; private set; }` alanı
eklenir; her durum değişiminde (`Create`, `Update`, `UpdateImageUrl`) aggregate içindeki bir
`RecalculateCompleteness()` metoduyla yeniden hesaplanır.
`IsComplete = !string.IsNullOrWhiteSpace(Description) && !string.IsNullOrWhiteSpace(ImageUrl)`.

**Rationale**:
- Marten sorguları (`SearchProducts`, `GetProduct`, `GetProductByName`) Postgres'e çevrilir;
  `IsComplete` kalıcı bir alan olduğu için WHERE koşulu (`x.IsActive && x.IsComplete`) doğrudan
  SQL'e çevrilir ve index-dostudur. Uçuşta hesaplanan getter-only bir property, whitespace-trim
  mantığını SQL'e çeviremez.
- Invariant yine aggregate'in içinde korunur: alan private setter'dır, yalnızca aggregate
  metotlarından yeniden hesaplanır; dışarıdan set edilemez (anayasa II).
- Eski/mevcut dokümanlarda alan yoksa Marten onu bool `false` olarak deserialize eder → o
  ürünler otomatik "eksik/satış dışı" sayılır ki bu doğru davranıştır (seed ürünleri zaten eksik).

**Alternatives considered**:
- *Getter-only computed `IsComplete`*: sorguda SQL'e çevrilemez; her sorguya ham alan
  koşulları (`Description != '' && ImageUrl != null ...`) elle yazmayı ve whitespace'i SQL'de
  ele almayı gerektirir — kural tekrar eder, anayasa II'yi zayıflatır. Reddedildi.
- *Ayrı bir read-model/projection*: bu ölçek için aşırı; tek bool alan yeterli. Reddedildi.

## Decision 2: "Satışta" nasıl ifade edilir?

**Decision**: "Satışta" ayrı bir kalıcı alan DEĞİL, `IsActive && IsComplete` bileşimidir.
Aggregate okunabilir bir `bool IsOnSale => IsActive && IsComplete;` computed property'si expose
eder (response'lar ve testler için). Sorgular bu bileşimi doğrudan iki kalıcı bool üzerinden
filtreler (`x.IsActive && x.IsComplete`), computed property'yi WHERE'de kullanmaz.

**Rationale**: İki kavram (aktiflik = admin niyeti, tamlık = veri bütünlüğü) ayrı kalır
(kullanıcı kararı). Ayrı bir `IsOnSale` alanı saklamak üçüncü bir senkronize-tutulacak durum
yaratırdı; türetme drift riskini ortadan kaldırır.

**Alternatives considered**: Kalıcı `IsOnSale` alanı — gereksiz durum çoğaltma, drift riski. Reddedildi.

## Decision 3: Hangi sorgular filtrelenir?

**Decision**:
- **`Agent/SearchProducts`** (müşteri + asistan keşif): `&& x.IsComplete` eklenir (bugün yalnızca `IsActive`).
- **`Agent/GetProduct`** (asistan, add_to_cart öncesi bilgi): `&& x.IsComplete` eklenir — satılamaz ürün sepete eklenemez.
- **`Queries/GetProductByName`** (müşteri `/search`): bugün yalnızca `!IsDeleted` filtreliyor; müşteri araması olduğundan `&& x.IsActive && x.IsComplete` eklenir (satılabilirlik kuralına hizalar). FR-003.
- **`Queries/GetAllProducts`** (admin listeleme): FİLTRELENMEZ; response'a `IsComplete` ve `IsOnSale` eklenir (US3/FR-006 — görünürlük).

**Kararla dışarıda bırakılan**:
- **`Queries/GetProductById`** (doğrudan id ile detay): filtrelenmez. Bu bir keşif/arama değil,
  doğrudan kayıt çekmedir ve admin/UI detay görünümlerince de kullanılır. Satılabilirlik zorlaması
  keşif (search) ve sepete-ekleme (GetProduct) noktalarında yapılır. İstenirse admin detayına
  `IsOnSale` bilgisi eklenebilir; bu feature için zorunlu değil.

**Rationale**: FR-003 "müşteri ve asistan **araması**" der; zorlama keşif ve satın-alma
girişindedir. Doğrudan id-lookup'ı kısıtlamak admin/UI detay akışlarını bozabilir; kapsam dışı tutulur.

## Decision 4: Test stratejisi

**Decision**: Yeni `tests/Catalog.Api.Tests` projesi (Basket.Api.Tests csproj pattern'i,
`Catalog.Api`'ye ProjectReference), slnx'e kaydedilir. Saf domain birim testleri (xUnit +
Shouldly) yalnızca `Product` aggregate davranışını doğrular: Create/Update/UpdateImageUrl
sonrası `IsComplete` ve `IsOnSale`. Sorgu filtreleri saf-domain harici olduğundan (Marten/host
gerektirir) birim test kapsamı dışıdır; anayasa "saf domain birim testi" ilkesine uyulur.

**Rationale**: Anayasa host/entegrasyon harness'ı olmayan saf domain testleri şart koşar.
Kuralın kalbi aggregate'te olduğundan, en yüksek değer domain testlerindedir. Sorgu filtreleri
`quickstart.md`'deki manuel/uçtan-uca senaryoyla doğrulanır.

**Alternatives considered**: Marten-in-memory/entegrasyon testi — anayasa saf domain diyor; ek
harness kaçınılır. Reddedildi.

## Decision 5: Migration / mevcut veri

**Decision**: Ayrı migration YOK. Marten şemasız doküman deposu olduğundan `IsComplete` alanı
yeni dokümanlara Create/Update ile yazılır; eski dokümanlarda yoksa `false` deserialize edilir
(satış dışı = doğru). Dev ortamı kalıcı volume kullansa da seed `alreadySeeded` ile korunur ve
seed ürünleri zaten eksik olduğundan davranış tutarlıdır.

**Rationale**: Ek işlem gerektirmez, güvenli varsayılan doğru yöne düşer.