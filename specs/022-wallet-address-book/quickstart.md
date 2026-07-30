# Quickstart / Validation — Wallet & AddressBook

Feature'ı uçtan uca doğrulayan çalıştırılabilir senaryolar. Ayrıntı: [data-model](./data-model.md),
[contracts](./contracts/).

## Ön koşullar

- .NET 10 SDK, Docker (Postgres/RabbitMQ Aspire ile).
- Geçerli dev sertifikası (`dotnet dev-certs https`) — IdP HTTPS zorunlu.
- Sistem **Aspire AppHost** ile: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
  (Customer.Api resource'u ayakta + `customerDb` migrate olmalı).

## Birim testleri (saf domain — host yok)

```bash
dotnet test tests/Customer.Api.Tests/Customer.Api.Tests.csproj
```

Kapsanan invariant'lar:
- Wallet: AddCard, RemoveCard, SetDefaultCard sonrası **en fazla 1** IsDefault (INV-1).
- AddressBook: aynı ≤1 varsayılan (INV-2); UpdateAddress geçmişi değiştirmez (izole VO).
- SavedCard tipi ham PAN/CVV alanı **içermez** (derleme/şema düzeyinde; INV-3).
- Geçmiş son-kullanma → AddCard Error (FR-009); boş adres → Create Error (FR-002).

## Senaryo A — Adres defteri (US1, gateway/kart olmadan)

1. Login (WebApp BFF). Adres ekle (POST `/api/v1/addresses`, `AddressInput`) → listede görünür.
2. İkinci adres ekle; birini varsayılan yap (POST `/addresses/{id}/default`) → önceki varsayılan
   düşer (yalnız 1 varsayılan).
3. Adresi düzenle (PUT) → güncellenir.
4. Adresi sil (DELETE) → listeden kalkar.
5. Boş alanla ekleme → 400 + doğrulama Result (kayıt yok).

**Beklenen**: her adım Result zarfıyla; SC-001 (tek form < 30 sn).

## Senaryo B — Cüzdan / kart (US2, simüle tokenize)

1. Kart ekle (POST `/api/v1/cards`, `AddCardInput` = pan/cvv/expiry/label). Stub tokenize eder;
   yanıt `{Id}`. GET `/cards` → yalnız brand+last4+expiry+label+isDefault (**token/PAN yok**).
2. İkinci kart ekle; birini varsayılan yap → yalnız o varsayılan.
3. Kartı sil → listeden kalkar; stub `RevokeAsync` çağrılır (no-op).
4. Geçmiş son-kullanma tarihli kart ekle → 400, kayıt yok (FR-009).
5. (Stub'ı tokenize hatası dönecek şekilde zorla) → kart kaydedilmez, hata döner (FR-013 fail-closed).
6. (Stub `RevokeAsync` hata fırlatacak şekilde zorla, kart sil) → **local silme yine başarılı**
   (revoke fail-open; gateway hatası silmeyi bozmaz).

**Beklenen**: SC-002 — hiçbir yanıtta ham PAN/CVV/token yok.

## Senaryo C — MCP okuma (ChatAgent)

1. ChatAgent'a "kartlarımı listele" / "adreslerimi göster" → `list_cards` / `list_addresses`
   tool'ları çalışır; yalnız güvenli alanlar döner.
2. "…1111 ile" gibi belirsiz referans birden çok karta uyuyorsa agent açık seçim ister (FR-018).
3. Kart ekleme tool'u **yok** (agent kart ekleyemez; FR-019).

## Senaryo D — Snapshot izolasyonu (US3 kontratı)

Bu feature snapshot'ı uygulamaz; doğrulama checkout feature'ında. Burada yalnız: kayıt
düzenlense/silinse Customer verisi bağımsız; Order kendi VO'sunu tuttuğundan geçmiş sipariş
etkilenmez (SC-004). Bkz. [mcp-and-snapshot](./contracts/mcp-and-snapshot.md).

## Manuel doğrulama ipuçları

- Postgres `customerManagement` şemasında `wallet` + `address_book` doküman tablolarını gör;
  kart satırında **hiçbir PAN/CVV kolonu/JSON alanı olmamalı**.
- Loglarda PAN/CVV grep'le → **hiç eşleşme olmamalı** (FR-008).