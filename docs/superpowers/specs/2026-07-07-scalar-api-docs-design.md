# Scalar API Dokümantasyonu — Tasarım

**Tarih:** 2026-07-07
**Durum:** Onaylandı

## Amaç

7 mikroservise (catalog, basket, order, discount, payment, stock, file) OpenAPI
belgesi ve Scalar tabanlı interaktif API dokümantasyon arayüzü eklemek. Şu an
projede hiç OpenAPI/Swagger yok; kurulum sıfırdan yapılır.

## Kapsam

- **Dahil:** 7 mikroservis (catalog, basket, order, discount, payment, stock, file).
- **Hariç:** Gateway (YARP) ve Identity.Server. Gateway'de birleştirilmiş (aggregate)
  Scalar sayfası YAGNI gereği yapılmaz. Identity zaten Duende UI'sine sahip.

## Mimari

Kurulum, `src/ServiceDefaults` içinde ortak iki extension olarak toplanır. 7 servis
zaten `ServiceDefaults.csproj`'u referans ediyor, dolayısıyla paketler ve kod tek
yerden gelir; csproj tekrarı olmaz.

### Paketler

`Directory.Packages.props`'a merkezî sürüm olarak eklenir:

- `Microsoft.AspNetCore.OpenApi` (net10.0 uyumlu sürüm)
- `Scalar.AspNetCore` (güncel kararlı sürüm)

Bu iki paket `Version` olmadan `PackageReference` olarak doğrudan
`ServiceDefaults.csproj`'a eklenir. Servis csproj'ları değişmez.

### Extension'lar

`src/ServiceDefaults` içine yeni bir dosya (`OpenApiExtensions.cs`), namespace
`Microsoft.Extensions.Hosting` (mevcut `Extensions.cs` ile aynı) içinde:

- `AddOpenApiDocumentation(this IHostApplicationBuilder builder)`
  → `builder.Services.AddOpenApi()`. Auth güvenlik şeması eklenmez (sade
  dokümantasyon kararı — Bearer denemesi yok).

- `MapScalarDocumentation(this WebApplication app)`
  → **yalnızca** `app.Environment.IsDevelopment()` iken:
  `app.MapOpenApi();` + `app.MapScalarApiReference();`. Production'da hiçbir
  dokümantasyon endpoint'i map'lenmez.

### Servis entegrasyonu

Her servisin `Program.cs`'ine iki satır eklenir:

- `builder` kurulumları arasında: `builder.AddOpenApiDocumentation();`
- `var app = builder.Build();` sonrasında: `app.MapScalarDocumentation();`

Servisler `AddServiceDefaults()`'u zaten çağırıyorsa oraya gömmek yerine ayrı iki
çağrı tercih edilir; böylece OpenAPI/Scalar açıkça görünür ve gerekirse tek bir
serviste kapatılabilir.

## Endpoint'ler ve erişim

- OpenAPI JSON: `/openapi/v1.json`
- Scalar UI: `/scalar/v1`

Servisler Aspire ile ayağa kalkınca, Aspire dashboard'undaki her servisin kendi
endpoint'inden doğrudan `/scalar/v1` ile açılır. Gateway üzerinden gitmeye gerek
yoktur.

## Bilinen incelikler

- **Sürümlü route'lar:** Endpoint'ler `api/v{version:apiVersion}/...` biçiminde.
  Yerleşik `Microsoft.AspNetCore.OpenApi`, `version` segmentini bir path
  parametresi olarak gösterir. Per-version ayrı belge üretimi (Asp.Versioning
  ApiExplorer entegrasyonu) YAGNI gereği eklenmez; öğrenme projesi için kabul
  edilebilir.
- **MCP endpoint'leri:** `MapMcp("/mcp")` OpenAPI'ye dahil olmaz (minimal API
  metadata taşımaz); yalnızca REST endpoint'leri belgelenir. Beklenen davranış.

## Test / Doğrulama

Repoda test altyapısı yok. Doğrulama:

1. `dotnet build ECommerceWithAgentFramework.slnx` — derleme geçmeli.
2. `dotnet run --project src/AppHost` — Aspire ile ayağa kaldır.
3. Bir servisin (ör. catalog) endpoint'inden `/scalar/v1` açılıp endpoint'lerin
   listelendiği doğrulanır; `/openapi/v1.json` geçerli JSON dönmeli.

## Yorumlar

Kod yorumları Türkçe yazılır (proje konvansiyonu).