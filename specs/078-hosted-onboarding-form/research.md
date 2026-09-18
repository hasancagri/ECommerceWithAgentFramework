# Research: Hosted Merchant Onboarding (078)

Phase 0 — Technical Context'te NEEDS CLARIFICATION kalmadı; aşağıdakiler tasarım kararlarının
gerekçe kaydıdır.

## D1 — Ekran host'u: Customer.Api içinde Minimal API + gömülü HTML

- **Decision**: Credential giriş ekranı Customer.Api'de iki Minimal API ucu olarak yaşar:
  `GET /merchant-credentials/{token}` (HTML form döner) + `POST /merchant-credentials/{token}`
  (doğrula + kaydet). HTML tek dosyalık gömülü şablondur (Razor Pages/SPA kurulmaz).
- **Rationale**: MerchantInformation ve kayıt slice'ı zaten bu BC'de; tek sayfa için UI framework'ü
  töreni gereksiz. WebApp geri getirilmez.
- **Link tabanı**: `CredentialEntryOptions.PublicBaseUrl` config'inden (077'de PG'nin hosted linki
  kendi dış URL'iyle üretmesi emsali). İstek HttpContext'i KULLANILMAZ: MCP tool çağrısı mcp-gateway
  proxy'sinden gelir; oradaki base Aspire service-discovery iç adresidir, tarayıcı çözemez.
- **Alternatives considered**: Razor Pages (altyapı yükü, tek sayfa için ağır); Identity.Server'a
  ekran koymak (yanlış BC — credential customer domain'i); ayrı mini UI servisi (yeni servis yasağı
  ruhuna aykırı, god-surface riski).

## D2 — Token mekanizması: Marten dokümanı, imza değil

- **Decision**: `CredentialEntrySession` aggregate'i customerDb'de saklanır: 256-bit rastgele URL-safe
  token (Id değil ayrı alan, `RandomNumberGenerator`), `ExpiresAt` (varsayılan 60 dk, Options'tan),
  `ConsumedAt`. Tek kullanım: POST başarısında `Consume()`; GET tüketmez (form açıp vazgeçme linki
  öldürmez, süre öldürür). İmzalı/self-contained token (JWT/DataProtection) KULLANILMAZ.
- **Rationale**: Tek-kullanımlık semantik sunucu-durumu İSTER (imzalı token'ı geri çekemezsin);
  Marten dokümanı hem TTL hem tüketim işaretini doğal taşır; ek kripto altyapısı gerekmez.
- **Alternatives considered**: ASP.NET DataProtection imzalı link (revoke/tek-kullanım yok);
  JWT (aynı sorun + boyut); HMAC + nonce tablosu (aynı DB ihtiyacı, daha çok parça).

## D3 — Store↔PG kanalı: S2S REST, mevcut makine kimliğiyle (clarify Q2=B)

- **Decision**: `PgOnboardingClient` (tipli HttpClient) PG Merchant.Api'ye REST konuşur; auth mevcut
  `OnboardingGatewayTokenHandler` (client_credentials, `ecommerce-onboarding`, DropShop Identity
  `connect/token`, cache'li Bearer) aynen `.AddHttpMessageHandler` ile takılır.
  `DropShopOnboardingOption.McpUrl` → `ApiBaseUrl` olur. `MerchantOnboardingClient` (imperatif MCP)
  ve `OnboardingResultParser` silinir.
- **Rationale**: 070 sapmasının gerekçesi (PG'ye dokunma yasağı) kalktı; 077 `PgHostedPaymentClient`
  emsaliyle tek desen; tipli sözleşme kırılgan MCP-text parse'ını bitirir.
- **Alternatives considered**: MCP imperatif sürer (sapma büyür, parser kırılgan); gRPC (dış realm'e
  iç desen dayatması + iki solution arası proto senkron yükü; anayasa dış tarafa HTTP bırakır).

## D4 — Onboarding başlatma: yeni `admin_start_onboarding`, PII'li tool'un yerine

- **Decision**: PII alan `admin_submit_onboarding` yerine parametresiz (yalnız e-posta alan)
  `admin_start_onboarding` gelir: PG'de form oturumu açar, hosted form URL'i döner. E-posta başvuru
  kimliği olarak kalır (070 kuralı). Eski tool canlı doğrulama sonrası sökülür (FR-009 sıralaması).
- **Rationale**: E-postayı store'un bilmesi tekilleştirme + durum sorgusu için şart; geri kalan tüm
  PII forma taşınır.
- **Alternatives considered**: E-postayı da formda almak (store durum sorgusunu kimliksiz bırakır;
  başvuru-store eşlemesi kopar).

## D5 — Credential doğrulama: kayıt anında PG'ye (clarify Q3=B)

- **Decision**: Ekran POST'unda store, PG'nin `POST /api/v1/onboarding/credentials/validate` ucuna
  makine token'ıyla gider; geçersizse kayıt reddedilir. PG erişilemezse kayıt `Unverified` işaretiyle
  saklanır, ekran bunu söyler (FR-013).
- **Rationale**: Yanlış yapıştırma anında yakalanır; sandbox test döngüsü kısalır.
- **Alternatives considered**: Yalnız biçim doğrulama (hata ilk ödemeye saklanır — clarify'da elendi).

## D6 — Ekran slice yerleşimi: kullanıcı-niyetli `Features/Commands`

- **Decision**: Ekran POST'u `Features/Commands/SubmitMerchantCredentials.cs` (admin tetikler,
  süreç değil); MCP tool'ları `Features/Agents/Commands|Queries`. Endpoint'ler
  `CredentialEntryEndpointExtension`'da, token doğrulaması endpoint katmanında.
- **Rationale**: conventions.md okuma testi: "kullanıcı mı tetikledi?" → evet (admin, tarayıcıdan) →
  Domains altında niyet yüzeyi; Saga/Process değil.
- **Alternatives considered**: gRPC-gömme kuralı (tek çağıran sanksiyonlu S2S değil — çağıran insan).

## D7 — Tool filtreleme tuzağı (070/074)

- **Decision**: Yeni admin tool'lar customer `/mcp-admin` oturum filtresine eklenir; customer
  filtrelemesi hangi mekanizmadaysa (prefix ya da açık allowlist) yeni adlar oraya işlenir —
  implementasyonda `ConfigureSessionOptions` koduna bakılıp uygulanır. Söküm sırasında eski adlar
  listeden düşülür.
- **Rationale**: 074 tuzağı: allowlist'e eklenmeyen tool oturumda görünmez — sessiz kayıp.

## D8 — Marten tr-TR alias tuzağı kontrolü

- **Decision**: Yeni doküman adı `CredentialEntrySession` büyük 'I' içermez → `DocumentAlias`
  gerekmez. (Tuzak kaydı: adında büyük I olan doc tr-TR makinede 42P07 üretir.)