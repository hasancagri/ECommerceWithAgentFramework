# Research: Wallet & AddressBook

Feature büyük ölçüde belirsizlik giderilmiş (spec Clarifications + Assumptions). Aşağıdaki
kararlar mevcut kod desenlerine ve constitution'a dayanır.

## D1: Bounded Context yerleşimi

- **Decision**: Yeni **Customer BC** (`Customer.Api`, `customerDb`, şema `customerManagement`).
  Wallet + AddressBook aynı serviste (profil verisi tek yer).
- **Rationale**: Spec Clarifications — Order BC değil, Payment BC değil. Payment yalnız
  işlem/charge; profil verisi Customer'da. Constitution II: bir BC birden fazla aggregate.
- **Alternatives**: (a) Order BC'ye koy — reddedildi (profil ≠ sipariş). (b) Payment BC'ye
  koy — reddedildi (Payment işlem odaklı, PCI yüzeyini şişirir).

## D2: Aggregate sayısı ve şekli

- **Decision**: İki aggregate root — `Wallet` (SavedCard koleksiyonu), `AddressBook`
  (SavedAddress koleksiyonu). Her ikisi `UserId`-keyli, UserId üzerinde Marten index.
  SavedCard/SavedAddress **sade entity** (base sınıf almaz), Basket'in `BasketItem` deseni.
- **Rationale**: İki bağımsız invariant (kart ≤1 varsayılan, adres ≤1 varsayılan) + iki ayrı
  yaşam döngüsü. Constitution II gevşetmesi (v1.3.0) çoklu aggregate'e izin verir. Tek
  aggregate'e sıkıştırmak iki ilgisiz koleksiyonu birleştirir — reddedildi.
- **Alternatives**: Tek "CustomerProfile" aggregate — reddedildi (yapay birleşim, ayrı
  değişim eksenleri).

## D3: "En fazla 1 varsayılan" invariant'ı

- **Decision**: `SetDefaultCard(id)` / `SetDefaultAddress(id)` aggregate metodu: hedefi bulur,
  diğerlerinin `IsDefault=false`, hedefin `true`. Tek yazma (aggregate = tutarlılık sınırı)
  eşzamanlı yarışı çözer; Marten optimistic/tek-doküman güncelleme atomik.
- **Rationale**: Constitution II — invariant handler'da değil aggregate'te. SC-003.
- **Alternatives**: Handler'da düzeltme — reddedildi (invariant sızar). Silmede otomatik terfi
  — reddedildi (spec edge: otomatik terfi yok, açık seçim).

## D4: Tokenize kontratı + stub

- **Decision**: `ICardTokenizer` soyut kontrat (`Task<TokenizeResult> TokenizeAsync(pan, cvv,
  expMonth, expYear, ct)`); bu iterasyonda `SimulatedCardTokenizer` (sahte opak token + marka
  algılama son4/BIN'den). DI marker (`ISingletonDependency`/`IScopedDependency`) ile kayıt.
  Gateway (ayrı repo) gelince yalnız stub gerçek çağrıyla swap; Wallet kodu değişmez.
- **Rationale**: Spec Assumptions + memory `payment-gateway-card-vault-direction`. PAN/CVV
  yalnız tokenize çağrısına girer, sonuç token; PAN/CVV saklanmaz/loglanmaz (FR-008).
- **Alternatives**: Gateway'i beklemek — reddedildi (US2 şimdi tam çıkar). Gerçek şifreleme
  bu repoda — reddedildi (vault ayrı repo'nun işi).

## D5: PCI — ham PAN/CVV yaşam döngüsü

- **Decision**: AddCard command'i PAN/CVV'yi parametre alır ama **aggregate'e/DB'ye yazmaz**;
  yalnız `ICardTokenizer`'a geçer. Command/Response/event/log alanlarında PAN/CVV yok.
  SavedCard yalnız Token+Brand+Last4+ExpMonth+ExpYear+Label+IsDefault tutar.
- **Rationale**: FR-007/008, SC-002. Kart ekleme **asla** MCP tool'u değil (ham PAN LLM'e
  girmez, FR-019).
- **Alternatives**: PAN'ı geçici sakla — reddedildi (PCI yüzeyi).

## D6: Fail-closed kayıt

- **Decision**: Tokenize başarısız/gateway erişilemez ise AddCard `Error` Result döner, hiçbir
  şey `Store` edilmez (yarım kayıt yok). Command handler `[Transactional]`.
- **Rationale**: FR-013, edge case. Basket'in fail-closed rezervasyon deseniyle tutarlı.

## D7: MCP yüzeyi (okuma-yalnız)

- **Decision**: `list_cards`, `list_addresses` MCP tool'ları — `Features/Agent/` slice'larını
  `IMessageBus` ile sarar. Yazma (ekle/sil/varsayılan) MCP'ye açılmaz. Kart görünümü yalnız
  marka+son4+son-kullanma+etiket (token dahil hassas alan yok).
- **Rationale**: FR-019, SC-002. BasketMcpTools deseni; okuma slice'ı `Features/Agent/`.
- **Alternatives**: Yazmayı da açmak — reddedildi (PAN/güvenlik + spec net).

## D7b: Kart silme → gateway token revoke (fail-open)

- **Decision**: DeleteCard (ve kart "güncelleme" = sil+yeniden ekle) eski token'ı gateway'de
  revoke etmeli. `ICardTokenizer.RevokeAsync(token)`; bu iterasyonda stub no-op. Handler önce
  local sil + `Store`, SONRA best-effort revoke — **fail-open** (gateway erişilemez ise local
  silme bloklanmaz; log/ileride retry).
- **Rationale**: Orphan token vault'ta chargeable kalır (PCI hijyeni). Delete ≠ Add: kullanıcı
  "kaldır" dedi, local silme otorite. Ters sıra (önce remote) gateway hatasında kullanıcıyı
  kilitler. Snapshot güvenli: token order'a kopyalanmaz. Bkz. Obsidian todo-payment-gateway-card-vault.
- **Alternatives**: Fail-closed revoke — reddedildi (gateway kesintisinde silme yapılamaz).
  Hiç revoke etme — reddedildi (orphan chargeable token; yalnız gateway sweep'ine güvenmek zayıf).

## D8: Kart update yok, adres update var

- **Decision**: AddressBook `UpdateAddress` destekler; Wallet update yok (kart değişimi =
  sil + yeniden ekle; son-kullanma güncellemesi de sil+ekle).
- **Rationale**: Spec Assumptions. Kart alanları token'a bağlı; kısmi güncelleme anlamsız.

## D9: Yetki scope'ları

- **Decision**: `customer.read` (listeleme) + `customer.write` (ekle/sil/varsayılan/update).
  `AuthorizationScopes` + Identity `Config.cs` (ApiScope + ApiResource `customer.api` +
  BFF `AllowedScopes`). Kullanıcı yalnız kendi UserId'sine erişir.
- **Rationale**: Constitution V, FR-014/015. Basket read/write deseni.

## D10: Checkout snapshot — yalnız kontrat

- **Decision**: Bu feature snapshot'ı **uygulamaz**; yalnız kontratı belgeler (kopyalanan
  alanlar: adres alanları + kart marka/son4). Checkout/ödeme akışı ayrı feature.
- **Rationale**: Spec US3 "yalnız referanslama + snapshot kontratı"; FR-016/017. Order zaten
  kendi `Address` VO'sunu snapshot tutar (mevcut CreateOrder deseni).