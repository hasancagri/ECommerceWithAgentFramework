# Data Model: Hosted Merchant Onboarding (078)

## Store (customerDb — bu repo)

### CredentialEntrySession (YENİ aggregate)

Tek kullanımlık, süreli credential-giriş ekran oturumu.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | doküman kimliği |
| Token | string | 256-bit rastgele, URL-safe; link `{base}/merchant-credentials/{token}` |
| RequestedByUserId | Guid | linki üreten admin (iz için) |
| ExpiresAt | DateTimeOffset | üretim + `CredentialEntryLinkLifetime` (varsayılan 60 dk) |
| ConsumedAt | DateTimeOffset? | başarılı POST anı; dolu ise oturum ölü |

**Davranış / invariant (test-first, İLKE VI):**
- `Create(userId, lifetime)` → token üretir, süre kurar.
- `Consume(now)` → `ResultDomain`: süresi geçmişse ya da `ConsumedAt` doluysa Error; değilse işaretler.
- `IsUsable(now)` saf getter (süre + tüketim kontrolü).

**Yaşam döngüsü:** Issued → Consumed (başarılı kayıt) | Expired (süre; pasif — ayrı işaret yok,
`ExpiresAt` geçmişse ölü). Yeni link istemek eskisini İPTAL ETMEZ (süre öldürür) — basitlik; aynı anda
birden çok yaşayan link kabul edilebilir risk (hepsi tek kullanımlık + kısa süreli).

### MerchantInformation (MEVCUT — alan eklenir)

| Alan | Değişiklik |
|---|---|
| MerchantId / MerchantKey | değişmez (doldurulma yolu değişir: chat → ekran) |
| CredentialsVerified | YENİ bool — PG doğrulaması geçtiyse true; PG erişilemezken kayıtta false (FR-013 "doğrulanamadı") |

**Davranış:** mevcut credential-set davranışı `verified` bayrağını da alır; iz (AdminActionLog)
yazımı handler'da sürer, key izde YER ALMAZ (FR-007).

### AdminActionLog (MEVCUT — yeni action türleri)

- `credential_entry_link_created` (linki üreten admin, token DEĞİL — yalnız oturum Id)
- `merchant_credentials_submitted` (ekran POST'u; MerchantId yazılır, MerchantKey YAZILMAZ)

## PG tarafı (kavramsal — ayrı repo, kontrat contracts/pg-onboarding-rest.md)

- **OnboardingApplication**: e-posta kimlikli; Pending → Approved | Rejected; approve'da teslim
  linki + mail üretir. Aynı e-postayla ikinci oturum: mevcut Pending başvuruya bağlanır.
- **FormSession**: store'un açtırdığı süreli (≈24 saat), tek başvuruluk hosted form erişimi.
- **RevealLink**: tek kullanımlık, süreli (≈1 saat) teslim linki; MerchantId+Key'i BİR KEZ gösterir;
  PG Admin yeniden üretebilir (eskisi ölür).