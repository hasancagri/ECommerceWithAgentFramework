# Contract: ProductChangedEvent (genişletilmiş)

- **Yer**: `src/others/Shared/IntegrationEvents.cs`
- **Taşıma**: RabbitMQ fanout (`RabbitMqConstants` — mevcut exchange, değişmez); tüketici kuyruğu `storefront.events` (Sequential).
- **Yön**: Catalog.Api → Storefront.Api (tek tüketici).

## Şekil

```csharp
public record ProductChangedEvent(
    Guid ProductId,
    string Name,
    string Description,   // YENİ
    decimal Price,        // YENİ
    string Brand,         // YENİ — BrandType enum adı (ör. "Apple")
    string? ImageUrl,
    bool IsDeleted);
```

## Yayın kuralları

- `CreateProduct` / `UpdateProduct`: aggregate'in güncel değerleriyle, `IsDeleted=false`.
- `DeleteProduct`: aggregate'in son değerleriyle, `IsDeleted=true`.
- Fat event ilkesi: tüketici hiçbir alan için geri çağrı yapmaz.

## Uyumluluk

- Kırıcı değişikliktir; yayıncı + tüketici aynı repo'da birlikte deploy olur (K1).
- Uçuştaki eski mesajlar dev ortamında ihmal edilir; eski satırlar dev reset + ingestion yeniden koşusuyla dolar.