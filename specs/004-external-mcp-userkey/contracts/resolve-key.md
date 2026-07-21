# Contract: Resolve Key (internal introspection)

Servislerdeki `ApiKeyAuthenticationHandler`, `X-User-Key`'i bu iç uç noktayla çözer.
Identity.Server barındırır. **İç trafik** — dış tüketiciye açılmaz (gateway route'u yok).

## Request

```
POST /api/keys/resolve
Host: <identity-server>            # servisler IdentityOption.Address ile ulaşır
X-Internal-Secret: <shared>        # v1 minimal koruma (D8; sertleştirme not düşüldü)
Content-Type: application/json

{ "key": "umk_<opak-anahtar>" }
```

## Responses

**200 OK** — anahtar geçerli ve iptalsiz. `scopes` kullanıcının **UserScopes**'undan gelir:

```json
{
  "userId": "b3f1…",
  "email": "ahmet@example.com",
  "givenName": "Ahmet",
  "familyName": "Yılmaz",
  "scopes": ["basket.write", "order.write"]
}
```

**401 Unauthorized** — anahtar yok / bilinmiyor / iptalli / kullanıcı pasif.
Gövde opsiyonel; handler yalnızca 200 dışını `Fail()` sayar.

## Notes

- Sunucu `key`'i **SHA-256**'lar, `KeyHash` unique index'inde arar (sabit-zamanlı karşılaştırma).
- İptalli (`IsRevoked=true`) veya kullanıcısı yok/pasif anahtar → 401.
- `scopes` anahtardan değil, **kullanıcının UserScopes** kayıtlarından türetilir (key scope taşımaz).
- **Cache yok** (D7) — yazma başına çözülür; iptal ≤ birkaç sn etkili.
- Handler eşlemesi: 200 → `AuthenticateResult.Success` (claims'ten principal); 401 → `Fail()`.