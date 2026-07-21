# Contract: Manage Keys (admin) + Register-time scope selection

Identity.Server'da barındırılan operatör (admin) uçları ve kayıt-anı scope seçimi.
Admin uçları `apikeys.manage` scope'u ile korunur (Q1=A, D8).

## Issue key (admin)

```
POST /api/keys
Authorization: Bearer <apikeys.manage taşıyan token>
Content-Type: application/json

{ "userId": "b3f1…", "name": "n8n-prod" }
```

**201 Created** — ham anahtar **yalnızca bir kez** döner (sonra yalnızca hash saklanır):

```json
{ "id": "…", "key": "umk_<opak>", "userId": "b3f1…", "name": "n8n-prod", "createdAt": "…" }
```

- Anahtar scope taşımaz; yetki kullanıcının UserScopes'undan gelir.
- Aynı kullanıcı için birden çok anahtar üretilebilir (FR-014).

## Revoke key (admin)

```
POST /api/keys/{id}/revoke
Authorization: Bearer <apikeys.manage taşıyan token>
```

**204 No Content** — anahtar iptal edildi (`IsRevoked=true`). Idempotent.
Sonraki resolve çağrıları bu anahtar için 401 döner (SC-002: ≤ 5 sn).

## Register-time scope selection (self-service)

Kayıt ekranı (`Account/Create`) operatör-tanımlı scope listesini checkbox olarak sunar.
Kullanıcı seçtikçe `UserScopes` yazılır.

- Sunulan küme **operatör** tarafından tanımlıdır (Config'te); kullanıcı yalnızca alt küme seçer.
- Listede olmayan scope edinilemez (FR-013).
- Seçim boşsa kullanıcı salt-okuma kalır (yazma reddedilir).

## Notes

- Operatörün `apikeys.manage` scope'unu nasıl edindiği (admin client/kullanıcı) tasks'ta netleşir.
- Ham anahtar yeniden gösterilemez; kaybolursa iptal + yeni anahtar üretilir.