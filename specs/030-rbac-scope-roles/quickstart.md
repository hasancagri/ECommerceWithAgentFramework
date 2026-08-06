# Quickstart / Doğrulama: RBAC — Rol = Scope Demeti

**Feature**: 030-rbac-scope-roles

Sistemi her zaman Aspire AppHost üzerinden çalıştır. Doğrulama = canlı senaryolar
(kontrat detayı için `contracts/role-management.md`, veri için `data-model.md`).

## Önkoşullar
- `dotnet build` temiz.
- Bootstrap admin email+parola config'te tanımlı (user-secrets/env; kodda değil).
- Temiz identityDb (seed'i sıfırdan görmek için) veya mevcut DB (backfill'i görmek için).

## Çalıştırma
```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

## Senaryolar

### S0 — Giriş noktası (D6)
1. Admin ile WebApp'e login ol.
2. **Beklenen**: header'da "Yönetim" linki görünür (token'da identity.roles.manage var).
3. Customer ile login ol → link GÖRÜNMEZ. Linke tıkla → IdP `/Admin/Roles` açılır (SSO).

### S1 — Seed + bootstrap admin (US4, FR-015/016/017)
1. Temiz identityDb ile başlat.
2. Bootstrap admin ile login ol (config'teki email/parola).
3. **Beklenen**: giriş başarılı; token admin scope'larını (identity.roles.manage dahil) taşır;
   rol yönetim ekranı açılır.
4. Sistemi yeniden başlat → duplike rol/kullanıcı/client OLUŞMAZ (idempotent).

### S2 — Register → customer → direkt login (US3, FR-013/014)
1. Yeni hesap kaydı yap.
2. **Beklenen**: aktivasyon adımı YOK; hemen login olunur.
3. Token'ın scope claim'i customer scope'larını taşır, admin scope'larını (ör. catalog.write,
   identity.roles.manage) TAŞIMAZ.

### S3 — Rol yönetimi + uyumsuzluk engeli (US2, FR-006/008/009/011)
1. Admin olarak rol yönetim ekranını aç → KnownScopes checkbox listesi görünür (serbest metin yok).
2. Yeni rol `editor` yarat; scope'ları işaretle (ör. catalog.write); kaydet.
3. Bilinmeyen bir scope string'i göndermeyi dene (elle istek) → REDDEDİLİR (INV-1).
4. `identity.roles.manage` OLMAYAN/normal customer ile yönetim ekranına eriş → 403 (FR-011).

### S4 — Rol atama scope'a yansır (US1/US2, FR-002/010/012)
1. Admin, bir customer kullanıcının rolünü `editor`'a çevir.
2. O kullanıcı **yeniden login** olur / token yeniler.
3. **Beklenen**: yeni token editor scope'larını (catalog.write) taşır; customer'a kapalı bir
   yazma ucu artık açık. Eski (elde kalan) token DEĞİŞMEZ — yansıma sonraki token'da.

### S5 — Downstream yalnız scope görür (US1, FR-003/004)
1. Herhangi bir kullanıcı token'ıyla bir servise git.
2. **Beklenen**: erişim scope claim'iyle belirlenir; token'da rol adı olsa bile yetki için
   kullanılmaz (servis kodu değişmedi).

### S6 — Son admin kilidi (Edge, FR-019)
1. Sistemde tek admin varken onun rolünü değiştirmeyi dene.
2. **Beklenen**: REDDEDİLİR (INV-4); sistem her zaman ≥1 admin bulundurur.

### S7 — Makine client (US4, FR-018)
1. IngestionAgent yazımları için `ingestion-agent` client_credentials token alır.
2. **Beklenen**: token catalog.write+stock.write taşır; rol/mail/kullanıcı YOK; rol süzgeci
   uygulanmaz (RBAC dışı).

## Birim testleri (Domain-TDD, İlke VI — test-first)
```bash
dotnet test --filter "FullyQualifiedName~Rbac"
```
- `ResolveGrantedScopes` → granted = requested ∩ roleBundle; geçersiz eşleme yazılmaz (INV-6).
- `ValidateAssignableScope` → bilinmeyen scope reddi (INV-1).
- `ApplySingleRole` → atama mevcut rolü değiştirir, rolsüz bırakmaz (INV-2/3).

## Başarı ölçütü eşlemesi
S0→SC-004 · S1→SC-001/006 · S2→SC-005 · S3→SC-003/004 · S4→SC-002 · S6→SC-007.