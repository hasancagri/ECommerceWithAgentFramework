# Quickstart: Admin Metinle Onboarding Doğrulaması

Amaç: admin'in metin ekranından doğal dille başvuru + durum sorgusu yapabildiğini; onboarding'in yalnız
`admin` persona'da olduğunu; yetkisiz erişimin reddedildiğini; challenge iki-adımının WebApp'te çözüldüğünü
kanıtlamak. Doğrulama E2E (Playwright) + elle.

## Ön koşullar

```bash
# ECommerce çözümü (WebApp + ChatAgent + Identity) Aspire ile:
dotnet run --project src/aspire/AppHost/AppHost.csproj
# DropShop gateway (Merchant.Api + Identity 5101) ayrı Aspire ile ayakta.
```

- **Admin kullanıcı**: bootstrap admin (seed) veya `admin` rolü atanmış kullanıcı (RBAC 030).
- **Config**: `DropShopGateway:McpUrl` = DropShop Merchant.Api `/mcp` (Aspire dashboard'dan dinamik port);
  WebApp onboarding MCP url'i ChatAgent'a tanımlı. Yoksa graceful-degrade (S4).

## Yapısal doğrulama (statik)

- **S0a**: `admin` persona ChatAgent'ta kayıtlı (`AddAIAgent("admin")`); tool seti yalnız onboarding MCP.
- **S0b**: Onboarding MCP tool'ları (`submit_registration`, `registration_status`) `assistant`/`public`
  persona tool setlerinde YOK.
- **S0c**: `dotnet build` 0 hata (WebApp + ChatAgent).

## Davranış senaryoları

**S1 — Admin metinle başvuru (P1)**
- Admin login → `/Admin/Onboarding` ekranı → "shop.example.com'u gateway'e kaydet" yaz.
- BEKLENEN: `admin` persona `submit_registration` MCP tool'unu çağırır → WebApp `RegisterAsync` iki-adımı
  (challenge dahil) çözer → yanıt "Pending / başvuru alındı" metniyle döner. Gateway'de RegisterRequest oluşur.

**S2 — Durum sorgusu (P2)**
- Aynı ekrana "shop.example.com başvurum ne durumda?" yaz.
- BEKLENEN: `registration_status` çağrılır → durum (AwaitingDomainControl/Pending/...) + Message metni döner.

**S3 — Yetki (RBAC)**
- Normal (customer) veya anonim kullanıcı `/Admin/Onboarding`'e / admin BFF ucuna erişmeye çalışır.
- BEKLENEN: reddedilir (ekran görünmez / endpoint 403). WebApp onboarding MCP yüzeyi de admin dışına kapalı.

**S4 — Graceful-degrade**
- WebApp onboarding MCP url'i yok/gateway erişilemez.
- BEKLENEN: ekran açılır; asistan "onboarding şu an kullanılamıyor" der; boot çökmez; diğer chat çalışır.

**S5 — Coexist (yapısal yol)**
- `POST /gateway-onboarding/register` doğrudan çağrılır (metin yolu dışı).
- BEKLENEN: mevcut davranış aynen çalışır (iki-adım otomatik Pending).

**S6 — Persona izolasyonu**
- Shopper (assistant) chat'inde "bir site kaydet" denenir.
- BEKLENEN: assistant'ta onboarding tool'u YOK; yapamayacağını söyler (onboarding yalnız admin persona).

## Kabul kanıtı

- S0a–S0c yapısal; S1–S2 davranış (SC-001); S3 yetki (SC-003); S4 degrade (SC-004); S5 coexist (SC-005);
  S6 izolasyon (SC-002). Config Options ile okunur (SC-006).
- E2E (Playwright): admin login → ekran → başvuru + durum akışı; yetkisiz erişim reddi. LLM tool-seçimi
  E2E dışı (elle/quickstart).