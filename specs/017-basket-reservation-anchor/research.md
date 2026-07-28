# Research: Basket Reservation Anchor (017)

Phase 0 çıktısı. Tüm belirsizlikler kod incelemesiyle çözüldü; NEEDS CLARIFICATION kalmadı.

## R1 — Proto'ya opsiyonel mutlak bitiş nasıl eklenir?

- **Decision**: `SetReservedQuantityRequest`'e `string expires_at = 4;` (ISO-8601 "O" formatı, boş = yok).
- **Rationale**: proto3'te yeni alan eklemek wire-uyumludur; eski istemci (Order) alanı hiç yazmaz →
  sunucuda boş string gelir → sabit TTL yolu. `ReservationReply.expires_at` zaten aynı string desenini kullanıyor.
- **Alternatives considered**: `google.protobuf.Timestamp` — ek import + dönüşüm; repo'daki mevcut desen
  (ISO-8601 string + `DateTimeOffset.TryParse`) ile tutarsız olurdu. Reddedildi.

## R2 — Stock: açık `expires_at` verilince mevcut rezervasyonun bitişi ne olur?

- **Decision**: Açık mutlak bitiş verildiyse HER durumda uygulanır (yeni rezervasyonda ctor ile, mevcutta set ile).
- **Rationale**: Çağıran (Basket) her zaman SABİT çapayı geçirir → "uygula" idempotenttir, rolling-TTL riski yok.
  Ayrıca kritik bug'ı çözer: süresi dolmuş ama süpürülmemiş rezervasyon `_reservations`'ta duruyor;
  eski davranışla (ExpiresAt yenilenmez) yeniden ekleme anında-dolmuş rezervasyon üretirdi (FR-008 kırılırdı).
- **Alternatives considered**: "Yalnız yeni/expired rezervasyonda uygula" — ekstra dallanma, aynı sonuç; basit kural seçildi.
  "Stock-side hizalama (kullanıcının tüm rezervasyonlarını hizala)" — spec aşamasında REDDEDİLDİ (BC sızıntısı).

## R3 — Çapa kuralları hangi katmanda yaşar?

- **Decision**: Kurallar `Basket` aggregate'inde: `ReservationExpiresAt` (persist), `IsExpiredAt(now)`,
  `PurgeExpiredItems(now)`, çapa kur/sıfırla metotları. Handler yalnız akışı örer (çapa adayı → gRPC → başarıda kur).
- **Rationale**: Anayasa İlke II — invariant aggregate'te. Çapa adayının Stock çağrısından ÖNCE bilinmesi
  gerekir (rezervasyon çapayla yaratılır); bu yüzden handler adayı hesaplar, başarıda aggregate'e yazar (FR-002).
- **Alternatives considered**: Çapayı Stock yanıtındaki `expires_at`'ten türetmek — ilk eklemede döngüsel
  bağımlılık yaratır (çapa isteğe girmeli); reddedildi.

## R4 — `BasketItem.ReservationExpiresAt` korunmalı mı?

- **Decision**: KALDIRILIR (entity, response'lar, DTO/VM'ler, `SetItem` parametresi dahil).
- **Rationale**: FR-009 satır bazında bitişi UI'dan kaldırır; tek doğruluk kaynağı sepet çapasıdır.
  Marten/Newtonsoft eski dokümandaki fazla JSON alanını sessizce yok sayar → migration gerekmez.
  Kullanıcı tercihi: dolaylama/ölü alan bırakma, düz kod.
- **Alternatives considered**: Alanı bırakıp UI'da görmezden gelmek — ölü veri + iki doğruluk kaynağı; reddedildi.

## R5 — Sepet süresi config'i nerede yaşar?

- **Decision**: Basket.Api'de `BasketReservationOptions` (`Basket` section, `ReservationDuration` varsayılan 5 dk).
- **Rationale**: FR-013; Stock'un `Reservations:Ttl`'i mekanizmanın geri düşüşüdür ve 15 dk kalır — iki ayrı
  BC'nin iki ayrı ayarı (çapa süresi Basket politikasıdır).
- **Alternatives considered**: Stock TTL'ini kullanmak — çapa süresi Basket kararı; BC sızıntısı olurdu. Reddedildi.

## R6 — Süresi dolmuş sepete ekleme (tembel temizlik) nasıl işler?

- **Decision**: Yazma handler'ları önce `basket.PurgeExpiredItems(now)` çağırır: dolmuş sepetse tüm satırlar
  düşer + çapa sıfırlanır; ardından normal akış (yeni çapa) koşar. Stock'a Release ÇAĞRILMAZ.
- **Rationale**: Çapa dolduysa Stock rezervasyonları da (aynı mutlak an) dolmuştur; sweep zaten süpürür.
  `ReservationExpired` handler'ı `RemoveItem` ile aynı satırları idempotent temizler — yarış zararsız.
- **Alternatives considered**: Release çağrısı eklemek — gereksiz ağ çağrısı, davranış farkı yok. Reddedildi.

## R7 — Eski (çapasız) sepetler ve rezervasyonsuz satırlar

- **Decision**: Migration yok. Çapasız + satırlı sepette banner çıkmaz (`ReservationExpiresAt == null`);
  ilk yeni başarılı ekleme çapayı kurar. Eski satırların Stock bitişleri kendi (eski) TTL'lerinde dolar.
- **Rationale**: Spec varsayımı; `ReservationExpired` zinciri eski satırları zaten tek tek temizler.
- **Alternatives considered**: Data-fix/migration — Marten dokümanlarını elle dolaşmak; değer katmıyor. Reddedildi.

## R8 — Banner ve sayaç davranışı (UI)

- **Decision**: Tablo üstü tek banner; sunucu `ReservationExpiresAt` + `IsReservationExpired` verir,
  istemci mevcut 1sn'lik tick desenini tek elemana uygular; dolunca "Expired" durumuna döner (FR-012).
- **Rationale**: Mevcut `reservation-countdown` JS deseni birebir yeniden kullanılır; checkout bloklanmaz.
- **Alternatives considered**: Sunucu taraflı kalan-süre (saniye) göndermek — istemci saati sapmasına karşı
  mutlak UTC + istemci tick mevcut ve kanıtlı desen; değiştirmek gereksiz. Reddedildi.

## R9 — Agent yüzeyi (MCP) etkisi

- **Decision**: `Features/Agent/GetBasket` response'una sepet düzeyi alanlar eklenir; per-item alan yoktu, kalkacak alan da yok.
  `Features/Agent/AddBasketItem` (rezervasyonsuz yol) 017 kapsamı DIŞINDA — davranışı değişmez, plan'da not edildi.
- **Rationale**: Agent'ın sepeti okurken süreyi görmesi tutarlılık sağlar; rezervasyonsuz agent-add 012'den beri
  bilinen ayrı bir borçtur, bu feature'ın kapsamını şişirmemek için ayrı tutuldu.
- **Alternatives considered**: Agent-add'i de rezervasyonlu yapmak — ayrı feature (scope creep). Reddedildi.