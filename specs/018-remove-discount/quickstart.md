# Quickstart: Discount Kaldırma — Doğrulama Rehberi (018)

## Önkoşullar

- Docker çalışıyor (Postgres + RabbitMQ container'ları için).
- `OpenAI:ApiKey` user-secrets'ta tanımlı (IngestionAgent fail-fast).

## 1. Statik doğrulama

```bash
dotnet build                        # 0 hata
dotnet test                         # tümü yeşil; Discount.Api.Tests artık yok
grep -ril "discount" src/ tests/ --include="*.cs" --include="*.json" --include="*.cshtml" \
  | grep -v "bin/\|obj/"            # beklenen: boş çıktı (SC-001)
```

## 2. Sistem ayağa kalkar (US1)

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

- Aspire panosunda `discount-api` ve `discountDb` resource'ları YOK.
- Tüm kalan servisler Running; hiçbirinin logunda discount servis-discovery hatası yok.

## 3. Alışveriş akışı indirimsiz (US2)

1. WebApp'e gir, login ol; vitrinde ürün kartlarında tek fiyat, indirim rozeti yok.
2. Ürünü sepete ekle; sepette kupon alanı yok, toplam = birim fiyat × adet.
3. Sipariş oluştur; özet/kayıtta indirim bilgisi yok, akış hatasız biter.

## 4. Ingestion 4 adımla biter (US3)

1. Supplier feed'ini tetikle (Hangfire pull veya bekle).
2. IngestionAgent logunda zincir: Brand → Category → Catalog → Stock → Finish (DiscountWrite yok).
3. Ürün Catalog + Stock'a yazılır; Storefront vitrinde görünür.
4. ChatAgent'ta araç listesinde `get_discount`/`apply_discount_coupon` yok; sohbet akışı çalışır.

## 5. Kimlik doğrulama sağlam

- Login akışı hatasız (Duende bilinmeyen scope reddetmiyor → scope talepleri temiz demektir).
- ChatAgent, kalan MCP sunucularına kullanıcı token'ıyla bağlanabiliyor.

## Beklenen sonuç

Spec SC-001..SC-004 sağlanır: kod tabanında anlamlı "discount" izi yok; sistem bir servis ve
bir DB eksik olarak tam işlevle çalışır; testler yeşildir.

## Temizlik (opsiyonel, dev)

- pgAdmin/psql ile `discountDb` volume kalıntısı silinebilir.
- RabbitMQ yönetim panosundan `discount.changed` exchange'i ve `discount.order-created` kuyruğu silinebilir.