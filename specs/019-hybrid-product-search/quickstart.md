# Quickstart: 019 Hibrit Ürün Araması — Canlı Doğrulama

## Önkoşullar

- Storefront için OpenAI config: `dotnet user-secrets set "OpenAI:ApiKey" "sk-..."` (Storefront.Api projesinde).
- Docker açık; eski postgres volume'ü pgvector imajıyla uyumlu (gerekirse volume sıfırla — ürünler feed'den geri gelir).

## Başlatma

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

- Beklenti: Storefront açılışta pgvector uzantısını/tabloyu kurar; OpenAI config yoksa fail-fast (FR-019).

## Veri besleme

- Supplier feed'ini tetikle (Hangfire cron veya manuel pull); ingestion ürünleri Catalog+Stock'a yazar,
  `ProductChangedEvent` Storefront'a düşer, embedding üretilir (US4, SC-003: 1 dk içinde aranabilir).

## Senaryolar

1. **Filtre (US1)**: REST — `GET {gateway}/v1/storefront/products/search?brands=X&brands=Y&minPrice=1000&maxPrice=3000&minStock=2`
   → yalnız uyan ürünler; stok 1 olan ürün listede yok.
2. **Kriter yok (US1-S4)**: parametresiz çağrı → 400 + doğrulama mesajı.
3. **Anlamsal (US2)**: `?searchText=kış sporlarında kullanılabilecek ayakkabı`
   → açıklaması anlamca uyan ürün üst sırada; alakasızlar eşik altı elenir.
4. **Hibrit (US3)**: `?searchText=kış sporu ayakkabısı&maxPrice=3000&minStock=1`
   → anlamca uyan ama 3000 üstü/stoksuz ürünler listede yok.
5. **Sohbet (FR-017/018)**: WebApp sağ alt sohbet — girişsiz "A veya B marka 1000-3000 arası ürünler"
   ve "kış sporları için ayakkabı arıyorum" → asistan listeyi detay linkleriyle döner.
6. **Tazelik (US4/SC-004)**: aynı feed'i tekrar besle → embedding üretimi tekrarlanmaz (log/DB kontrol);
   yalnız stok değişiminde de üretim yok.
7. **Dayanıklılık (SC-005)**: OpenAI anahtarını geçersiz yapıp yeniden başlat — açılış fail-fast;
   arama anı hatası için: anahtar geçerli açıl, sonra ağ kes → searchText'li arama hata Result'ı,
   filtre-yalnız arama çalışır.

## Kontrol noktaları

- `storefrontManagement` şemasında embedding tablosu ve `vector` uzantısı (pgAdmin).
- ChatAgent log'unda storefront MCP sunucusundan tool keşfi; public agent'ta Catalog `search_products` yok.
- Testler: `dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj`.