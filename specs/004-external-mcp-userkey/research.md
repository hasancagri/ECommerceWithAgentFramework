# Research: External MCP UserKey

Faz 0 — spec'teki gereksinimler ve mevcut kod tabanı ışığında çözülen tasarım kararları.

## D1 — Anahtar formatı ve saklama

- **Decision**: Anahtar = kısa prefix (`umk_`) + base64url(32 rastgele bayt). Ham değer **sunulmaz**;
  DB'de yalnızca **SHA-256 hash** saklanır, lookup hash ile yapılır.
- **Rationale**: Yüksek-entropili rastgele anahtar tahmin/türetilemez → sahtecilik yok (SC-005, FR-010).
  Yüksek entropili olduğu için parola gibi yavaş hash (bcrypt) gerekmez; SHA-256 yeterli ve hızlı.
- **Alternatives**: UserId'yi string'e gömme → imzasız sahtecilik açığı, imzalı → JWT'yi yeniden
  icat edip iptal yeteneğini kaybetme. Ham saklama → sızıntıda kullanılabilir anahtar. Reddedildi.

## D2 — Anahtar sahipliği ve BC izolasyonu

- **Decision**: `ApiKeys` tablosu Identity.Server `ApplicationDbContext`'inde, `AspNetUsers`'a FK.
  Servisler tabloya erişmez; anahtarı **HTTP resolve uç noktası** ile çözer.
- **Rationale**: Anayasa I — servisler `IdentityDbContext`'e dokunamaz; kimlik Identity.Server'ın işi.
  Mevcut `ApplicationUser`/Identity tabloları da burada (tutarlı yer).
- **Alternatives**: Anahtarı bir servise koymak → BC ihlali + kimlik dağılımı. Reddedildi.

## D3 — Custom authentication şeması (JWT yok)

- **Decision**: `Common`'da `ApiKeyAuthenticationHandler` (`AuthenticationHandler<Options>`). `X-User-Key`
  header'ını okur, resolve uç noktasına sorar, dönen kullanıcı claim'leri + verilen scope'larla
  `ClaimsPrincipal` kurar. Şema adı `ApiKey`.
- **Rationale**: `CurrentUser.Load` claim'leri **principal'den** okuyor (JWT'den değil), o yüzden
  handler aynı principal'i lookup'tan kurabilir → servis/handler kodu değişmez. Kullanıcı JWT/OAuth
  dansı istemiyor (FR-002, US1).
- **Alternatives**: Gateway'in JWT basması → kullanıcı "JWT istemiyorum" dedi. Her serviste elle
  handler yerine ortak infra tercih edildi (tekrarsız, `AuthenticationExtension` gibi).

## D4 — İki şemanın bir arada çalışması (forward policy scheme)

- **Decision**: Default şema olarak bir **forward "smart" policy scheme** eklenir: istek `X-User-Key`
  header'ı taşıyorsa `ApiKey` şemasına, yoksa `Bearer`'a yönlendirir (`ForwardDefaultSelector`).
- **Rationale**: Mevcut scope policy'leri (`RequireAuthenticatedUser` + `RequireClaim("scope", …)`)
  default şema üzerinden çalışıyor. Forward şeması sayesinde policy'ler iki kimlik türüyle de
  değişmeden çalışır; iç ChatAgent (JWT) ve dış anahtar (ApiKey) aynı endpoint'leri kullanır.
- **Alternatives**: Her policy'ye `AuthenticationSchemes` elle eklemek → dağınık, kırılgan. Reddedildi.

## D5 — NoResult / Fail semantiği ve "geçersiz anahtar read'de de reddedilir"

- **Decision**: Handler: header yok → `NoResult()` (anonim; read'ler geçer). Header var, çözülemez
  (yok/iptal) → `Fail()`. Ek olarak authentication'dan **sonra** küçük bir middleware: istekte
  `X-User-Key` var **ama** kullanıcı authenticated değilse → **401**, endpoint anonim olsa bile.
- **Rationale**: `AuthenticateResult.Fail` tek başına anonim endpoint'te 401 üretmez; spec edge-case
  "geçersiz anahtar okuma da dahil reddedilir" (FR-009) için terminal kontrol gerekir.
- **Alternatives**: Sadece Fail'e güvenmek → geçersiz anahtarlı read sessizce anonim geçerdi. Reddedildi.

## D6 — Yetki kaynağı: kullanıcıya bağlı UserScopes (rol yok)

- **Decision**: Scope **kullanıcıya** bağlanır — yeni `UserScopes` tablosu `(UserId, Scope)`.
  Kullanıcı **kayıtta** operatör-tanımlı listeden seçer. `ApiKeys` scope taşımaz; key kullanıcıya
  işaret eder ve onun scope'larını miras alır. Resolve, kullanıcının UserScopes'unu döndürür.
- **Rationale**: Anayasa V — rol yok, yalnızca scope. Scope'u kullanıcıya bağlamak, kullanıcı
  onayıyla en az ayrıcalık kurar (US4, FR-013). Bir kullanıcının tüm key'leri aynı seti paylaşır.
- **Alternatives**: (a) Key başına scope → yönetim dağılır, kullanıcı onayı yok. (b) Rol→scope→user
  (RBAC) → anayasa V'i bozar, amendment + sistem-geneli iş gerektirir. İkisi de reddedildi.
- **Sonuç**: Kayıt ekranı (Identity.Server `Account/Create`) scope seçimi kazanır; bu, feature'ı
  kayıt akışına dokunacak biçimde küçük ölçüde genişletir.

## D7 — İptal ve önbellek dengesi

- **Decision**: Resolve **yazma başına** çağrılır; agresif cache yok. İptal `IsRevoked=true` (veya
  `RevokedAt`) ile; resolve iptalli anahtarı reddeder. İptal ≤ birkaç sn etkili (SC-002).
- **Rationale**: Read'ler anonim → anahtar hot-path'te değil; yazmalar seyrek + zaten DB transaction.
  Yazma başına resolve maliyeti ihmal edilebilir; cache olmayınca iptal doğal olarak anında.
- **Alternatives**: Uzun-TTL cache → iptali geciktirir (gereksinime aykırı). Reddedildi.

## D8 — Uçların korunması

- **Decision (admin issue/revoke)**: Yeni `apikeys.manage` ApiScope; uçlar bu scope ile korunur
  (admin-only). Operatörün bu scope'u nasıl edindiği (admin client/kullanıcı) küçük ayrıntı,
  tasks'ta netleşir.
- **Decision (resolve)**: İç uç; Aspire iç ağında servisler çağırır. Kullanıcının UserScopes'unu
  döner. v1'de yapılandırılmış paylaşılan gizli header ile korunur; sertleştirme not düşülür.
- **Rationale**: Q1=A admin-only uçlar. Resolve iç-servis trafiği; mevcut ertelenmiş-auth duruşuyla
  uyumlu minimal koruma, üretim sertleştirmesi ayrı iş.
- **Alternatives**: Resolve'u tamamen açık bırakmak → anahtar-oracle riski. Reddedildi (minimal koruma).

## D9 — Okuma yüzeyini anonimleştirme kapsamı (Q2=A)

- **Decision**: Dış erişim **MCP yüzeyi** üzerinden. Read MCP tool'ları/`Features/Queries` handler'ları
  `[RequiredScope]` taşımaz ve `/mcp` gateway route'u auth'suz → okumalar zaten anonim erişilebilir.
  Anahtar verilirse read yine kişiselleşir (ör. `get_basket` o kullanıcının sepeti). Anahtar yoksa
  anonim (Guid.Empty) → kullanıcıya özel read'ler boş/anonim döner.
- **Rationale**: Q2=A tüm okumalar anonim. REST API gateway policy'leri (ClientCredential/Password)
  WebApp iç yolu; bu feature kapsamı dışı, dokunulmaz.
- **Alternatives**: REST read'leri de anonimleştirmek → WebApp auth modeline sızar, kapsamı şişirir.