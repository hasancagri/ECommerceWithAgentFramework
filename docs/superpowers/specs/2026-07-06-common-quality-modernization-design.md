# Common Kütüphanesi — Kalite / Modernizasyon Refactor'ı

**Tarih:** 2026-07-06
**Kapsam:** `src/Common`
**Amaç:** Genel kalite/modernizasyon. Davranış değişikliği yok, yeni özellik yok.

## Motivasyon

`src/Common` çapraz-kesen kütüphane; tüm servisler ona bağlı. Zamanla biriken
tutarsızlıklar var: solution'ın geri kalanından farklı bir hedef framework,
karışık namespace stili, Nullable açık olmasına rağmen onunla çelişen kod ve
birkaç yanıltıcı/ölü parça. Bu refactor bunları davranışı değiştirmeden temizler.

## Doğrulama edilmiş ön koşullar

- `DateHelper.Now`'un solution genelinde **hiç çağıranı yok**.
- `Enumeration.FromValue` / `Enumeration.FromDisplayName`'in **dış çağıranı yok**.
- `Feature*` result tiplerine **28 dosya** `global using Common;` üzerinden erişiyor.
- `BaseModel` / `BaseUserTrackModel`'e Common dışında **tek** dosya referans veriyor;
  tüketici servisler zaten `global using Common.Domains;` içeriyor.

## Kapsam

### A — Hedef framework hizalama
- `src/Common/Common.csproj`: `<TargetFramework>net9.0</TargetFramework>` → `net10.0`.
- Solution'daki diğer 14 proje zaten `net10.0`; bu tek uyumsuzluğu giderir.

### B — Namespace / stil tutarlılığı
- Blok-kapsamlı namespace kullanan 13 dosyayı **file-scoped** namespace'e çevir.
- `Results/BaseClasses/` altında `namespace Common` olan sınıfları
  `namespace Common.Results.BaseClasses`'e taşı.
- `Results/` altında `namespace Common` olan arayüzleri `namespace Common.Results`'e taşı.
- `BaseModel` / `BaseUserTrackModel`: `namespace Common` → `namespace Common.Domains`.
- **Karar (onaylı):** Yaygın kullanılan `Feature*` result tipleri
  (`FeatureResultModel`, `FeatureObjectResultModel`, `FeatureListResultModel`,
  `FeaturePagedResultModel`) `namespace Common`'da **kalır**. Ergonomik giriş
  noktası; 28 tüketici dosyada değişiklik gerektirmez.
- **`MessageItem` de `namespace Common`'da kalır.** `Results/BaseClasses/`
  altında olmasına rağmen 10 tüketici ona `global using Common;` ile erişiyor.
  Base sınıflar `Common.Results.BaseClasses`'e taşınsa bile onu iç-içe namespace
  (enclosing-namespace) araması sayesinde `using` olmadan görmeye devam eder.
- Namespace taşımaları sonrası Common içinde derlemeyi bozan yerlere gereken
  `using` satırları eklenir (derleyici yönlendirir). Tüketici projelerde
  değişiklik beklenmez.

### C — Nullable doğruluğu
- `Exceptions/GlobalExceptionHandler.cs`: `FeatureResultModel apiResultModel = null;`
  → `= null` kaldırılır. `switch` her dalda (default dahil) atama yaptığı için
  definite-assignment sağlanır.
- `Domains/Enumeration.cs`:
  - `public override bool Equals(object obj)` → `Equals(object? obj)`.
  - `public int CompareTo(object obj)` → `CompareTo(object? obj)`.

### D — Ölü / yanıltıcı kod
- `Utils/Helpers/DateHelper.cs`: `Now(string? culture = "Europe/Istanbul")` imzası
  yanıltıcı (parametre yok sayılıyor, hep `UtcNow` dönüyor). Parametre kaldırılır:
  `public static DateTime Now() => DateTime.UtcNow;`. Çağıran olmadığı için risksiz.
- `Domains/Enumeration.cs`: `FromValue` / `FromDisplayName` kendi fırlattıkları
  exception'ı akış kontrolü için try/catch ile yakalıyor. `Parse` metodu throw
  yerine `T?` döndürecek (`FirstOrDefault`) şekilde sadeleştirilir; iki metot da
  try/catch olmadan sonucu döndürür. Genel davranış (bulunamazsa `null`) korunur.

## Kapsam dışı (bu refactor'da yapılmayacak)

- **E** — `FeatureOutputModel.cs` içindeki result sınıflarının konsolidasyonu /
  dosya adının içerikle eşleştirilmesi / `NotFound`/`Error` boilerplate de-dup'ı.
- Herhangi bir davranış değişikliği veya API değişikliği.
- Yeni test eklenmesi (solution'da test projesi yok).
- Kapsam dışı ölü kod avı (seçilen kapsam A+B+C+D ile sınırlı).

## Doğrulama

- Her mantıksal adımdan sonra:
  `dotnet build ECommerceWithAgentFramework.slnx` **uyarısız/temiz** geçmeli.
- Namespace taşımalarından sonra tüm solution derlenmeli (tüketici servisler dahil).
- Otomatik test yok; doğrulama derleme + statik incelemeyle sınırlı.

## Riskler

- **Düşük.** Tüm değişiklikler mekanik veya kullanılmayan koda dokunuyor.
- Tek dikkat noktası: namespace taşımalarında Common-içi `using`'lerin eksiksiz
  eklenmesi — derleyici hataları anında yakalar.