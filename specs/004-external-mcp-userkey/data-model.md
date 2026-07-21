# Data Model: External MCP UserKey

Yeni kalıcılık yalnızca **Identity.Server** `ApplicationDbContext` (EF Core, Postgres) içinde.
Servislerin veritabanları değişmez (Anayasa I). İki yeni tablo: `ApiKeys`, `UserScopes`.

## Entity: ApiKey

Identity.Server EF Core entity'si; `AspNetUsers`'a bağlı. Marten aggregate'i **değildir**.
**Scope taşımaz** — yetki kullanıcının `UserScopes`'undan gelir.

| Alan | Tip | Kural / Not |
|------|-----|-------------|
| `Id` | `Guid` (PK) | Kayıt kimliği. |
| `KeyHash` | `string` | Anahtarın **SHA-256** hash'i (base64/hex). Benzersiz index. Ham anahtar saklanmaz. |
| `UserId` | `string` (FK → `AspNetUsers.Id`) | Anahtarın temsil ettiği gerçek kullanıcı. Zorunlu. |
| `Name` | `string?` | Operatör için etiket (ör. "n8n-prod"). Opsiyonel. |
| `IsRevoked` | `bool` | İptal bayrağı. Resolve yalnızca `false` olanları döndürür. |
| `CreatedAt` | `DateTime` (UTC) | Üretim zamanı. |
| `RevokedAt` | `DateTime?` (UTC) | İptal zamanı (audit). |

- **Expiration alanı yoktur** — anahtar iptal edilene dek süresizdir (FR-003).
- `KeyHash` üzerinde **unique index**; resolve `KeyHash` ile arar (SC-005 sabit-zamanlı karşılaştırma önerilir).

## Entity: UserScope

Kullanıcının kayıtta seçtiği yetki. Kullanıcının efektif scope setini oluşturur.

| Alan | Tip | Kural / Not |
|------|-----|-------------|
| `Id` | `Guid` (PK) | Kayıt kimliği. |
| `UserId` | `string` (FK → `AspNetUsers.Id`) | Scope'un sahibi kullanıcı. Zorunlu. |
| `Scope` | `string` | Operatör-tanımlı bir scope değeri (ör. `basket.write`). |

- `(UserId, Scope)` **benzersiz** — aynı scope kullanıcıya iki kez eklenmez.
- Yalnızca **operatör-tanımlı** scope kümesinden değer alır (FR-013); rastgele scope girilemez.

## Davranış (entity üstünde)

- `ApiKey.Create(userId, keyHash, name)` → yeni aktif anahtar (`IsRevoked=false`, `CreatedAt=now`).
- `ApiKey.Revoke()` → `IsRevoked=true`, `RevokedAt=now`. Idempotent (zaten iptalliyse no-op).

## İlişkiler

- `ApiKey* → 1 ApplicationUser`. Bir kullanıcının **birden çok** anahtarı olabilir; birinin iptali
  diğerini etkilemez (US2 senaryo 3). Tüm key'ler kullanıcının aynı UserScopes setini paylaşır (FR-014).
- `UserScope* → 1 ApplicationUser`. Kullanıcı silinir/pasifleşirse resolve anahtarı reddeder.

## Çözümlenmiş kimlik (resolve çıktısı, kalıcı değil)

Resolve uç noktası anahtardan bir **claims görünümü** döndürür (handler principal kurar):

| Claim | Kaynak |
|-------|--------|
| `sub` | `ApiKey.UserId` |
| `email` | `AspNetUsers.Email` |
| `given_name` / `family_name` | Kullanıcı profili (varsa) |
| `scope` (0..n) | Kullanıcının `UserScopes` kayıtları |

`CurrentUser.Load` bu principal'den `Id`/`Email`/`Name` okur; `ScopeAuthorizationMiddleware`
`scope` claim'lerini kontrol eder — mevcut mekanizma değişmeden çalışır.