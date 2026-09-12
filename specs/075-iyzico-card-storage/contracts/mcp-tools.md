# Contract: MCP Tools (müşteri kart yüzeyi)

Customer BC `/mcp` (login-korumalı, customer scope). Tümü ince sarmalayıcı → `Features/Agents/*` slice.
Ham PAN/CVV **hiçbir** argümanda/yanıtta yok.

## add_card

Standalone kart ekleme oturumu başlatır; kullanıcıya tarayıcıda açacağı hosted-form linkini döner.

- **Args:** yok (kullanıcı token'dan `UserId`).
- **Döner:** `{ addUrl: string, sessionId: guid, expiresAt: datetime }`
- **Davranış:** PG add-session çağır → link + conversationId; `AddCardSession(Pending)` yaz.
- **Kullanım notu (Description):** "Linki tarayıcıda aç, kartı iyzico ekranında gir. Bittiğinde
  'kartlarımı listele' de — yeni kart görünür."

## list_cards

Kullanıcının kayıtlı kartlarını PG'den **canlı** listeler.

- **Args:** yok.
- **Döner:** `CardView[] = [{ cardHandle, brand, last4, expiryMonth, expiryYear, alias, isDefault }]`
- **Davranış:** `Wallet.PgUserHandle` yoksa boş liste. Varsa PG list-cards → izdüşüm. Cache YOK.
- **Kısıt:** PAN/CVV asla; `cardHandle` opak (sil/varsayılan için).

## delete_card

Kayıtlı kartı siler (tarayıcı yok).

- **Args:** `{ cardHandle: string }`
- **Döner:** `{ deleted: bool }`
- **Davranış:** PG delete-card(PgUserHandle, cardHandle). Silinen kart varsayılansa
  `Wallet.ClearDefaultIfMatches`. Kullanıcı-handle sahipliği doğrulanır (FR-008).

## set_default_card

Bir kartı varsayılan yapar.

- **Args:** `{ cardHandle: string }`
- **Döner:** `{ isDefault: bool }`
- **Davranış:** `Wallet.SetDefaultCard(cardHandle)` (≤1 varsayılan). cardHandle kullanıcının
  listesinde olmalı (PG list ile doğrula).

## place_order (mevcut — FR-014 onay guard'ı)

Çekim öncesi açık onay zorunlu.

- **Args:** mevcut + `confirmed: bool = false` (MCP optional-default tuzağı: default `false`).
- **Davranış:** `confirmed=false` → `Result.Error` (çekim başlamaz). Description **kanonik** onay
  talimatı taşır: "Önce kullanıcıya tutar + kartın son 4 hanesini göster, onay al, sonra
  `confirmed:true` ile çağır." Çekim varsayılan kartla (aksi belirtilmezse).

## Güvenlik/oturum notları

- Tümü `RequireAuthorization` (customer). Anonim erişim yok (upfront login).
- `add_card` callback ucu MCP tool DEĞİL — ayrı HTTP ucu (bkz s2s/callback sözleşmesi), JWT'siz,
  conversationId korelasyonlu.