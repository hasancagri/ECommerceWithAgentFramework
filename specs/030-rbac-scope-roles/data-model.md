# Data Model: RBAC — Rol = Scope Demeti

**Feature**: 030-rbac-scope-roles | **Store**: EF Core + Postgres (identityDb)

Identity.Server bir domain BC değil, IdP altyapısıdır; bu nedenle veriler ASP.NET Identity +
OpenIddict tabloları + tek yeni EF entity üzerinden modellenir (Marten/aggregate değil).

## Varlıklar

### Role — `AspNetRoles` (mevcut, ASP.NET Identity `IdentityRole`)
| Alan | Tip | Not |
|------|-----|-----|
| Id | string (GUID) | PK |
| Name | string | Benzersiz rol adı (ör. `admin`, `customer`) |
| NormalizedName | string | Identity yönetir |

- Seed rolleri `admin`, `customer` silinemez (uygulama kuralı, D5).
- Yeni rol admin ekranından yaratılır (US2).

### UserRole — `AspNetUserRoles` (mevcut, çok-çok)
| Alan | Tip | Not |
|------|-----|-----|
| UserId | string | FK → AspNetUsers |
| RoleId | string | FK → AspNetRoles |

- **Tek-rol invariant'ı uygulama katmanında** (D4): atama önce mevcut rolleri kaldırır.
  Şema çok-çok kalır; kural kod ile korunur.

### RoleScope — `RoleScopes` (YENİ EF entity)
Bir rolün bir KnownScope'a bağlanması. Rol→scope demetini oluşturur.

| Alan | Tip | Kural |
|------|-----|-------|
| Id | Guid | PK |
| RoleId | string | FK → AspNetRoles; rol silinince cascade |
| Scope | string | KnownScopes içindeki bir scope adı OLMALI (FR-006) |

- Benzersizlik: `(RoleId, Scope)` unique — aynı scope bir role iki kez eklenemez.
- Yazımda doğrulama: `Scope ∈ KnownScopes` değilse REDDEDİLİR.
- İlişki: Role 1—* RoleScope.

### KnownScope — kod registry (tablo DEĞİL)
Atanabilir scope'ların kapalı, kod-sahipli listesi. DB'de tutulmaz; koddan okunur.

| Alan | Tip | Not |
|------|-----|-----|
| Name | string | Scope adı (ör. `catalog.write`, `identity.roles.manage`) |
| Description | string | Ekranda gösterilen insan-okur açıklama (FR-007) |

- Kaynak: servis scope sabitleri (`AuthorizationScopes`) + `Config.AllApiScopes`.
- Yeni üye: `identity.roles.manage` (rol yönetim yüzeyi scope'u).
- Kullanım: (a) ekran checkbox kaynağı, (b) RoleScope yazımında doğrulayıcı, (c) seed map kaynağı.

### ApplicationUser — `AspNetUsers` (mevcut)
- Değişmez; rol ilişkisi UserRole üzerinden. Bootstrap admin bu tabloya seed'lenir.

### OpenIddict Application (client) — mevcut OpenIddict tabloları
- Değişmez şema; seed'e `ingestion-agent` client eklenir (client_credentials, catalog.write +
  stock.write). `order-saga`, `ecommerce.bff`, `apikeys.admin` zaten var.

## İlişki Özeti
```
AspNetUsers 1 ── * AspNetUserRoles * ── 1 AspNetRoles 1 ── * RoleScopes
                                                    (RoleScope.Scope ∈ KnownScopes[kod])
Tek-rol kuralı: bir User'ın AspNetUserRoles satırı DAİMA tam olarak 1 (uygulama zorlar)
```

## Doğrulama Kuralları (test edilebilir invariant'lar)
- **INV-1 (KnownScopes kapalılık)**: `RoleScope.Scope` KnownScopes dışındaysa yazım reddedilir.
- **INV-2 (tek rol)**: rol atama sonrası kullanıcının rol sayısı tam olarak 1'dir.
- **INV-3 (rolsüz kalmama)**: rol atama mevcut rolü değiştirir; kullanıcı hiç rolsüz kalmaz.
- **INV-4 (son admin)**: admin rolündeki tek kullanıcının rolü değiştirilemez (FR-019).
- **INV-5 (seed rolü koruma)**: `admin`/`customer` silinemez; kullanıcısı olan rol silinemez.
- **INV-6 (scope çözümleme)**: token'a yazılan API scope'ları = `requested ∩ roleBundle`;
  KnownScopes'tan düşmüş (artık geçersiz) eşlemeler token'a yazılmaz.

## Saf birim adayları (Domain-TDD, İlke VI — test-first)
- `ResolveGrantedScopes(requested, roleBundle, knownScopes)` → INV-6 (saf fonksiyon).
- `ValidateAssignableScope(scope, knownScopes)` → INV-1.
- `ApplySingleRole(currentRoles, newRole)` → INV-2/INV-3 (saf dönüşüm).