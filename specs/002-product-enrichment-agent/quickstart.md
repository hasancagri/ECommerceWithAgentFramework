# Quickstart: Product Enrichment Agent (canlı doğrulama)

Uçtan uca doğrulama. Enrichment agent, açıklanması/görseli eksik ürünleri MCP ile
tamamlar; ürün tam olunca (aktifse) satışa çıkar. SC-002: seed 30 ürün, satışa-hazır 0→≥28.

## Önkoşul

1. **OpenAI anahtarı** (chat + gpt-image-1 erişimi). Agent'a user-secrets ile ver:

   ```bash
   cd src/agents/ProductEnrichmentAgent
   dotnet user-secrets init
   dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
   dotnet user-secrets set "IdentityOption:ClientSecret" "enrichment-secret"
   ```

2. Docker çalışıyor (Aspire Postgres + RabbitMQ container'ları için).

## Çalıştır

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

- Aspire dashboard'da `enrichment-agent` resource'u `identity-server` + `gateway`
  hazır olunca başlar; ~10 sn sonra eksik ürünleri çeker ve sırayla işler.
- Logları izle: `Enrichment: N eksik urun bulundu` → ürün başına sonuç → özet sayaç.

## Doğrula

1. **Satışa-hazır sayısı** (SC-002): koşu bitince Catalog'da `IsOnSale` ürün sayısı
   0'dan ≥28'e çıkmalı. WebApp ana sayfası / arama ürünleri satışta göstermeli.
2. **Gerçek görsel** (SC-003): `http://localhost:<gateway>/file/images/{ProductId}.png`
   açılınca gerçek, görüntülenebilir bir ürün görseli gelmeli (placeholder değil).
3. **Açıklama** (FR-002): her tamamlanan üründe boş olmayan, ≤100 karakter açıklama.
4. **İdempotency** (SC-006): AppHost'u durdurup yeniden çalıştır — ikinci koşuda tam
   ürünler atlanır (`aciklama=Skipped, gorsel=Skipped`), yeni OpenAI/File üretimi olmaz.
5. **Kısmi başarı** (SC-005): bir alan üretilemezse ürün eksik kalır, satışa çıkmaz.

## Sorun giderme

- **401/403**: `enrichment.agent` client'ı Identity.Server'da tanımlı mı, secret eşleşiyor mu.
- **Görsel gelmiyor**: gateway `file-images-route` + File.Api `UseStaticFiles(/images)` aktif mi.
- **Token hatası**: `IdentityOption:Address` HTTPS ve issuer ile eşleşmeli (dev cert bypass açık).