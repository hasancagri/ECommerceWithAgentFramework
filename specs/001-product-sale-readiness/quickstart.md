# Quickstart / Validation: Product Sale Readiness

Bu feature'ın uçtan uca çalıştığını doğrulayan senaryolar. Uygulama **her zaman Aspire
AppHost üzerinden** çalıştırılır.

## Ön koşullar

- .NET 10 SDK
- Repo kökünde derleme: `dotnet build`

## 1. Domain birim testleri (birincil doğrulama)

Kuralın kalbi `Product` aggregate'inde; en hızlı ve kesin doğrulama birim testleridir.

```bash
# Yeni test projesi (bu feature'da eklenir)
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj
```

**Beklenen davranış (testlerin doğruladığı):**
- Boş açıklama VEYA boş görselle oluşturulan ürün → `IsComplete == false`, `IsOnSale == false`.
- Açıklama + görsel dolu, aktif ürün → `IsComplete == true`, `IsOnSale == true`.
- Tam ürünün açıklaması boşaltılınca → `IsComplete == false`, `IsOnSale == false`.
- `UpdateImageUrl` ile görsel dolunca (açıklama zaten dolu) → `IsComplete == true`.
- Tam ama `Deactivate()` edilmiş ürün → `IsComplete == true` ama `IsOnSale == false`.

## 2. Uçtan uca (Aspire) doğrulama — sorgu filtreleri

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

Sistem ayağa kalkınca (Catalog seed 200 eksik ürün oluşturur):

1. **Müşteri araması eksikleri göstermez (SC-001, SC-003):**
   - Asistan/müşteri arama akışı (SearchProducts) ile herhangi bir seed ürünü ara.
   - **Beklenen:** hiçbir seed ürünü satışta görünmez (hepsi `IsComplete=false`).

2. **Admin hepsini görür, durum ayırt edilir (SC-005):**
   - `GET` Catalog "tüm ürünler" (GetAllProducts) çağrılır.
   - **Beklenen:** 200 ürün de listelenir; her biri `IsComplete=false`, `IsOnSale=false`.

3. **Tamamlanınca satışa çıkar (SC-002):**
   - Bir ürünü `Update` ile hem açıklama hem görsel doldurarak güncelle (ürün aktif).
   - Aynı ürünü müşteri aramasıyla tekrar ara.
   - **Beklenen:** ürün artık satışta görünür.

4. **Tamlık, aktifliğin yerine geçmez (SC-004):**
   - Tam bir ürünü `Deactivate` et; müşteri aramasıyla ara.
   - **Beklenen:** görünmez (tam ama pasif).

## Başarı kriterleri eşlemesi

| Senaryo | Doğruladığı SC |
|---------|----------------|
| Birim testleri | FR-001, FR-002, FR-004, FR-005 |
| E2E 1 | SC-001, SC-003 |
| E2E 2 | SC-005 |
| E2E 3 | SC-002 |
| E2E 4 | SC-004 |

## Notlar

- Bu feature eksik ürünleri **doldurmaz**; seed ürünleri E2E'de elle güncellenerek
  "tamamlanınca satışa çıkma" doğrulanır. Otomatik doldurma ayrı bir feature'dır (spec Out of Scope).