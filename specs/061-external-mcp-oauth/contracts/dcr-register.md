# Kontrat: Dynamic Client Registration (RFC 7591) — Identity.Server

`POST /connect/register` (anonim; Content-Type: application/json). Claude Code bu ucu AS discovery
dokümanındaki `registration_endpoint` alanından bulur (OpenIddict discovery'ye event ile eklenir).

## İstek (Claude Code'un gönderdiği tipik alanlar)

```json
{
  "client_name": "Claude Code",
  "redirect_uris": ["http://localhost:PORT/callback"],
  "grant_types": ["authorization_code", "refresh_token"],
  "response_types": ["code"],
  "token_endpoint_auth_method": "none",
  "scope": "basket.read basket.write ..."   // opsiyonel
}
```

## Doğrulama kuralları (saf `DcrRequestValidator` — test-first, İlke VI)

- `redirect_uris` ZORUNLU, her biri şu kalıplardan birine uyMALI: `http://localhost:<port>/...`,
  `http://127.0.0.1:<port>/...`, `https://claude.ai/api/mcp/auth_callback`,
  `https://claude.com/api/mcp/auth_callback`. Aksi → `invalid_redirect_uri`.
- `grant_types` yalnız `authorization_code` + `refresh_token` alt kümesi; `client_credentials`
  istenirse → `invalid_client_metadata`.
- `token_endpoint_auth_method`: `none` (public). Confidential istek → `invalid_client_metadata`.
- `scope`: verilmişse dış-agent demetiyle KESİŞİMİ alınır; verilmemişse demetin tamamı atanır.
  Demet dışı scope sessizce düşürülür (registry kapalı — İlke V).

## Yanıt (201)

```json
{
  "client_id": "<opak>",
  "client_name": "Claude Code",
  "redirect_uris": ["http://localhost:PORT/callback"],
  "grant_types": ["authorization_code", "refresh_token"],
  "response_types": ["code"],
  "token_endpoint_auth_method": "none",
  "scope": "openid profile email offline_access storefront.read basket.read ..."
}
```

- `client_secret` DÖNMEZ (public client). Kayıt `ConsentType=Explicit` ile oluşur.
- Hatalar RFC 7591 biçimi: `{"error": "...", "error_description": "..."}` + 400.

## Notlar

- Uç anonimdir (Claude kayıt anında kimliksiz). Kötüye kullanım sınırı bu dilimde: yalnız izinli
  redirect kalıpları + kapalı scope demeti + public-only. Rate-limit tünel/public aşamasının işi.
- Aynı `client_name` tekrar kayıt olabilir (Claude her kurulumda yeni client üretebilir);
  temizlik/GC bu dilim dışı.