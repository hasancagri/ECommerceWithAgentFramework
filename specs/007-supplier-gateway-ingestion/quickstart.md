# Quickstart: Supplier Gateway + State'siz Ingestion — canlı doğrulama

Önkoşul: `dotnet run --project src/aspire/AppHost/AppHost.csproj` (Postgres + RabbitMQ + tüm servisler).
RabbitMQ management UI adresi Aspire dashboard'daki `rabbitmq` resource'unda.

## S1 — İlk çekim: tüm kayıtlar akar (US1 + US2)

1. Aspire dashboard'dan `supplier-gateway` loglarını izle; ilk çekimi bekle veya elle tetikle:
   `POST http://<supplier-gateway>/v1/feeds/pull` → `202`.
2. Bekle: `ingestion.supplier-product-snapshot` kuyruğu dolar ve boşalır (agent tüketir).
3. Doğrula: WebApp ana sayfasında feed ürünleri vitrinde; stok/indirim rozetleri dolu.
4. Doğrula (DB): `supplierGatewayManagement.mt_doc_feedsnapshot` satır sayısı = feed kayıt sayısı.
5. Doğrula: IngestionAgent'ın Postgres bağlantısı YOK (Aspire'da ingestionDb resource'u kalmadı).

## S2 — Değişmemiş feed: hiçbir şey akmaz (US1 / SC-003)

1. Feed'i değiştirmeden tekrar tetikle: `POST /v1/feeds/pull` → `202`.
2. Doğrula: kuyruğa mesaj düşmez (management UI publish rate 0); Catalog/Stock/Discount logları sessiz.

## S3 — Tek alan değişikliği: yalnız o kayıt akar (US1/3. senaryo)

1. `src/services/supplier/Supplier.Api/Datasets/products.json` içinde TEK ürünün fiyatını değiştir
   (dosya restart'sız okunur).
2. Tetikle → `202`. Doğrula: kuyruğa 1 mesaj düşer; vitrinde yalnız o ürünün fiyatı güncellenir.
3. İndirim kaldırma: aynı üründe `discountPercent`'i null yap → tetikle → vitrinde indirim rozeti kalkar.

## S4 — Geçici hata: retry kurtarır (US3/1. senaryo, SC-005)

1. Aspire dashboard'dan `discount-api`'yi durdur.
2. Feed'de indirimli bir ürünü değiştir → tetikle. Agent loglarında yazım hatası + retry görülür.
3. `discount-api`'yi geri başlat. Doğrula: mesaj retry ile işlenir, kuyruk boşalır, vitrin doğru.

## S5 — Kalıcı hata: DLQ'da görünür (US3/2-3. senaryolar, SC-006)

1. Feed'e bozuk kayıt ekle (ör. `brand`'i Catalog'un tanımadığı bir değer yap) → tetikle.
2. Doğrula: retry'lar tükenince mesaj `ingestion.supplier-product-snapshot.dlq`'ya düşer;
   management UI'da gövdesi (kayıt + hata bilgisi) incelenebilir.
3. Kaydı düzelt → tetikle (içerik değişti, yeniden yayınlanır) → vitrine düşer. DLQ'daki eski
   mesaj isteğe bağlı elle temizlenir/geri kuyruklanır.

## S6 — Yeniden teslim zararsız (SC-007)

1. Management UI'dan DLQ'daki (veya kuyruktaki) bir mesajı ana kuyruğa yeniden yayınla (requeue).
2. Doğrula: domain'lerde nihai durum değişmez; kopya ürün oluşmaz (upsert SKU'da yakınsar).

## S7 — Temizlik doğrulaması (US4)

1. `dotnet build` + `dotnet test` temiz geçer; `IngestionAgent` içinde Staging/Run/Scheduler/Feed
   tipleri ve `Marten` referansı kalmadığı görülür.
2. `/v1/ingestion/runs` artık yok (agent'ta HTTP yüzeyi yalnız health).

Beklenen sonuçların kaynağı: [spec.md](spec.md) başarı ölçütleri, kontratlar için `contracts/`.