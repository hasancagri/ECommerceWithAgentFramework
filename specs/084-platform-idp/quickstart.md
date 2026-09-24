# Quickstart — Platform IdP Doğrulama (Dilim A)

Amaç: `AgentPlatform`'a taşınmış, uygulama-nötr platform IdP'nin çalıştığını + ikinci-app config'le
tanınabildiğini kanıtla. Canlı kullanıcı/veri yok → hedef "doğru çalışıyor", "eskisiyle bire bir" değil.

## Ön koşul
- `AgentPlatform` repo'su + kendi Aspire AppHost'u (`AgentPlatform/src/AppHost`).
- ECommerce AppHost, `IdentityOption.Address` = AgentPlatform issuer URL'ine bakar.
- `Platform.Auth` paketi her iki repoda tüketilir (sürüm props'ta pinli).

## S1 — IdP standalone boot (SC-001)
1. `AgentPlatform` AppHost'u başlat.
2. Bekle: IdP + `identityDb` ayağa kalkar; seed (`SeedHostedService`) app-registry üzerinden scope + client + rol seed eder.
3. **Beklenen:** açılış hatasız; `/.well-known/openid-configuration` issuer'ı config'teki URL.

## S2 — Login + token + korumalı erişim (SC-003)
1. ECommerce AppHost'u başlat (IdP dış-servis olarak bağlı).
2. Kayıtlı kullanıcıyla login → token al.
3. Korumalı bir ECommerce yüzeyine token'la eriş.
4. **Beklenen:** login çalışır; token geçerli; scope zorlaması doğru (yetkili 200, yetkisiz 403).

## S3 — Downstream scope görür, rol görmez (FR-004)
1. Bir downstream servisin scope kontrolünü tetikle (yetkili + yetkisiz scope).
2. **Beklenen:** karar yalnız scope'a göre; rol adı değişse de scope aynıysa sonuç aynı.

## S4 — DCR + consent (dış AI istemcisi)
1. RFC 7591 DCR akışıyla dış agent kaydı + consent.
2. **Beklenen:** kayıt + consent çalışır; loopback/Claude callback muafiyeti bugünkü gibi (yeni loopback-only bağ eklenmedi).

## S5 — İkinci-app config tanıma (SC-002)
1. `AppRegistry:Apps`'e `pg` (Prefix `pg`, Scopes `pg.merchant`,`pg.commission.admin`) EKLE. ECommerce kodu değiştirme.
2. IdP'yi tekrar başlat.
3. **Beklenen:** açılış başarılı; `pg.*` scope'ları `KnownScopes.All`'da görünür.

## S6 — Ad-uzayı çakışması reddi (SC-004)
1. `pg` app'ine `catalog.write` (ECommerce'e ait) ekle.
2. IdP'yi başlat.
3. **Beklenen:** açılış REDDEDER (kesişim ihlali; net hata).