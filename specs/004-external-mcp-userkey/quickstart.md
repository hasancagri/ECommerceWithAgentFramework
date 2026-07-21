# Quickstart: External MCP UserKey — uçtan uca doğrulama

Bu kılavuz feature'ın çalıştığını kanıtlayan senaryoları listeler. Kod içermez;
ayrıntılar için [data-model.md](./data-model.md) ve [contracts/](./contracts/).

## Ön koşullar

- Sistem Aspire ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`.
- Identity.Server HTTPS'te (bkz. anayasa V / identity-server-https).
- Migration'lar uygulandı (`ApiKeys`, `UserScopes` tabloları oluştu).
- En az bir kayıtlı kullanıcı; kayıtta `basket.write` scope'u seçilmiş olsun.

## Senaryo 1 — Anahtarla kullanıcı adına yazma (US1, SC-001)

1. Admin uç: kullanıcı için anahtar üret (`POST /api/keys`) → dönen ham `umk_…`'yi al.
2. Gateway üzerinden yazma MCP tool'unu çağır (ör. sepete ekle), `X-User-Key: umk_…` ile.
3. **Beklenen**: işlem o kullanıcının verisinde gerçekleşir; başka token/adım yok.

## Senaryo 2 — Anahtarsız yazma reddedilir (US1 senaryo 2, FR-007)

1. Aynı yazma tool'unu **anahtarsız** çağır.
2. **Beklenen**: 401 / yetkisiz — anonim yazamaz.

## Senaryo 3 — Geçersiz/kurcalanmış anahtar reddedilir (SC-005, FR-009)

1. Geçerli anahtarın bir karakterini değiştir, yazma çağır.
2. **Beklenen**: 401 — başka kullanıcı olunamaz. Aynı bozuk anahtarla **okuma** da 401 (D5).

## Senaryo 4 — Anonim okuma (US3, SC-004)

1. Hiç anahtar göndermeden bir okuma tool'u çağır (ör. ürün listesi).
2. **Beklenen**: 200, veri döner — kimlik bilgisi gerekmez.

## Senaryo 5 — Kayıtta scope seçimi sınırlar (US4, FR-013)

1. Yalnızca `basket.read` seçmiş yeni bir kullanıcı için anahtar üret.
2. O anahtarla `basket.write` gerektiren bir yazma çağır.
3. **Beklenen**: 401 — kullanıcı o scope'u seçmediği için yetkisiz.

## Senaryo 6 — İptal anında etkili (US2, SC-002)

1. Çalışan bir anahtarla yazma yap → başarılı.
2. Admin uç: anahtarı iptal et (`POST /api/keys/{id}/revoke`).
3. Aynı anahtarla tekrar yazma → **Beklenen**: ≤ 5 sn içinde 401.

## Senaryo 7 — Süresizlik (SC-003)

1. Anahtarın oluşturma zamanından bağımsız olarak (exp alanı yok) yazma başarılı olmalı.
2. **Beklenen**: yenileme/expiration adımı hiç yok; yalnızca iptal durdurur.

## Senaryo 8 — Çoklu anahtar bağımsızlığı (US2 senaryo 3, FR-014)

1. Aynı kullanıcı için iki anahtar üret; birini iptal et.
2. **Beklenen**: iptalli 401; diğeri çalışır; ikisi de aynı UserScopes setini kullanır.