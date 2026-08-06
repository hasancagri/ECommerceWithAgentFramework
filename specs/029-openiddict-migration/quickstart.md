# Quickstart: Canlı Doğrulama (SC-002 smoke listesi)

**Date**: 2026-08-06 | **Plan**: [plan.md](plan.md)

## Ön koşullar

- `dotnet build` temiz; sistem HER ZAMAN AppHost'tan: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Docker volume sıfırlanmış olmalı (temiz DB — geçişte veri taşınmaz); ürünler feed'den yeniden dolar.
- Adım 4'ten önce adım 7 ile bir kullanıcı kaydedilir (temiz DB'de "kayıtlı kullanıcı" register'la oluşur).
- Dev sertifikası sağlıklı olmalı (`dotnet dev-certs https`); Identity.Server https://localhost:5001.

## Smoke sırası

1. **Discovery**: `https://localhost:5001/.well-known/openid-configuration` açılır; issuer birebir,
   `prompt_values_supported` içinde `create` var.
2. **Token biçimi**: `order-saga` client'ıyla `/connect/token`dan client-credentials token alınır (curl);
   jwt.io/decode ile bakılır — `scope` claim'i DİZİ (stock.reserve + basket.write), `aud` içinde stock.api + basket.api,
   şifreleme yok, `iss` birebir.
3. **Anonim gezinme** (US3): oturumsuz tarayıcıda ana sayfa + ürün listesi + ürün detay — login istemez (M2M anonim okuma çalışıyor).
4. **Mevcut kullanıcı login** (US1/FR-001): geçiş öncesi kullanıcının e-posta/şifresiyle WebApp login — başarılı; profil görünür.
5. **Scope'lu yazma** (US1): sepete ürün ekle (gRPC stok rezervi tetiklenir — `stock.reserve` bearer forwarding kanıtı).
6. **Checkout saga** (US4): sipariş tamamla — sipariş onaylanır, stok düşer, sepet temizlenir (order-saga m2m token kanıtı).
7. **Register** (US2): temiz tarayıcıda "Kayıt ol" → `prompt=create` ile `/Account/Create` açılır; hesap açılır, girişle alışveriş.
   Yeni kullanıcıda rol claim'i OLMADIĞI doğrulanır (FR-011).
8. **Oturum yenileme** (US1): login sonrası access token süresi dolunca (veya kısaltılmış TTL ile) 401→refresh→retry çalışır.
9. **ChatAgent** (US4): login'li kullanıcı asistandan sepetiyle ilgili işlem ister — per-user MCP tool çağrısı başarılı.
10. **Yetkisiz istek** (SC-004): token'sız/eksik-scope'lu istek (ör. Basket yazma) 401/403 döner — bugünkü davranış.
11. **ApiKeys admin**: `apikeys.admin` token'ı + `X-Internal-Secret` ile issue/revoke uçları çalışır.

## Beklenen sonuç

- 11 adımın tamamı PASS → SC-001..SC-004 sağlandı.
- `git diff --stat` yalnız Identity.Server + Directory.Packages.props (+ spec dosyaları) gösterir → SC-003 kanıtı.

## Bilinen kabuller

- Geçiş anında açık oturumlar düşer; 4. adımda yeniden login normaldir (tek seferlik).
- MailPit/aktivasyon YOK — Feature 2'nin konusu.