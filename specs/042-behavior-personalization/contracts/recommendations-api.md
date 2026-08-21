# Kontrat: Personalization REST API (v1)

Sunucu: Python FastAPI (`personalization` Aspire resource'u). Kimlik doğrulama YOK (anonim vitrin
okumalarıyla tutarlı; bkz. plan Constitution Check V). Tüm uçlar JSON döner.

## GET /v1/recommendations

Kişiye özel top-N ürün önerisi. Asla boş dönmez (FR-013; tek istisna: depo tamamen boşken).

**Query parametreleri**

| Param | Tip | Zorunlu | Not |
|-------|-----|---------|-----|
| userId | guid | – | login'li kullanıcı |
| anonymousId | guid | – | userId yoksa zorunlu |
| sessionProductIds | guid list (virgüllü) | – | oturumda gezilen ürünler (anonim yol için) |
| count | int | – | varsayılan 10, max 50 |

**Yanıt 200**

```json
{
  "productIds": ["9c8d...", "7b6a..."],
  "source": "personal",
  "modelTrainedAt": "2026-08-21T14:00:00Z"
}
```

- `source`: `personal` (modelde tanınan kimlik) | `session` (oturum ürünlerinden benzerlik) |
  `popular` (fallback). `modelTrainedAt`: fallback'te null.
- Sıra önem sırasıdır; skorlar dışa verilmez.

**Hatalar**: `userId` ve `anonymousId` ikisi de yoksa 400. Diğer her durumda 200 + fallback.

## POST /v1/admin/ingest (dev aracı)

JSONL ingest turunu hemen tetikler. Yanıt: `{"processedLines": n, "skippedLines": m}`.
Emsal: Procurement `POST /v1/feeds/pull` (anonim dev tetiği).

## POST /v1/admin/train (dev aracı)

Eğitim job'ını hemen tetikler. Yanıt: `model_runs` satırının özeti
(`{"status":"Succeeded","eventCount":123,"userCount":4,"itemCount":37}`).

## GET /health

Aspire sağlık ucu: `{"status":"ok","modelLoaded":true,"lastIngestAt":"..."}`.

## Evrim

- URL-segment sürümleme (`/v1/...`) — sistem konvansiyonuyla uyumlu.
- WebApp "Sana önerilenler" şeridi (ayrı feature) bu kontratın ilk gerçek tüketicisi olacak.