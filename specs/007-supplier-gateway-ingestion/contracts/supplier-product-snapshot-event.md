# Contract: SupplierProductSnapshotReceived (integration event)

**Yaşadığı yer**: `Shared.IntegrationEvents` (bilinçli paylaşılan sözleşme).
**Yayıncı**: Supplier.Gateway. **Tüketici**: IngestionAgent. Başka tüketici yok (ilk sürüm).

## Taşıma (RabbitMQ, Wolverine üzerinden)

| Öğe | Ad | Not |
|-----|-----|-----|
| Exchange | `supplier.product-snapshot` | Fanout (repo konvansiyonu) |
| Queue | `ingestion.supplier-product-snapshot` | Durable; IngestionAgent dinler |
| DLQ | `ingestion.supplier-product-snapshot.dlq` | Retry tükenince mesaj içeriğiyle düşer |

Adlar `RabbitMqConstants.SupplierProductSnapshot` altında merkezileşir.

## Şema

```csharp
public record SupplierProductSnapshotReceived(
    string SupplierCode,      // ilk sürümde "supplier"
    string ExternalId,        // Catalog SKU'su olarak kullanılır
    string Name,
    string Description,
    string Brand,             // doğrulama tüketici zincirinde (Catalog BrandType)
    decimal Price,
    int StockQuantity,        // mutlak stok (full snapshot)
    decimal? DiscountPercent  // 0-100; null = indirim yok
);
```

## Semantik

- Mesaj = kaydın tedarikçideki GÜNCEL hali. Diff/patch değildir; tüketici alan bazlı fark aramaz.
- Teslim at-least-once'tır: tüketici aynı mesajı iki kez işleyebilir; tüm yazımlar yakınsamalıdır.
- Sıra garantisi tek kuyruk/tek tüketici ile fiilen korunur; kontrat sıralamaya SÖZ VERMEZ.
- Yayın koşulu: Gateway'in son yayınladığı snapshot'tan farklı veya hiç yayınlanmamış olmak.
- Geri kanal yok: tüketici sonucu yayıncıya bildirmez ("tamamlandı" işareti tutulmaz).

## Evrim kuralları

- Alan ekleme: sona, mümkünse opsiyonel — eski tüketici kırılmamalı.
- Tedarikçi başına tip türetilmez; farklılaşma önce SupplierCode + opsiyonel alanla denenir.
- Kaygıya göre bölme (ürün/stok ayrı event) ikinci tedarikçi geldiğinde değerlendirilir (spec varsayımı).