# Kontrat: ModerationAgent Çıktısı (044)

In-process sözleşme (BC-içi): ModerationAgent (MAF ChatClientAgent) → `ModerateReview` handler.
041 EnrichmentAgent emsali: Singleton, Temperature=0, structured JSON output, MCP'siz.

## Girdi (prompt'a giden)

- Yalnız yorum METNİ (+ yıldız bağlam için). Kullanıcı adı/Id GÖNDERİLMEZ (gereksiz PII).
- Metni boş yorum agent'a HİÇ GİTMEZ: handler doğrudan temiz-verdict uygular (LLM çağrısı yok).

## Çıktı (structured JSON, zorunlu şema)

```json
{ "violation": true, "category": "insult", "reason": "kısa gerekçe (≤200)" }
```

| Alan | Tip | Kural |
|---|---|---|
| violation | bool | zorunlu |
| category | enum | `profanity` \| `insult` \| `personal_attack` \| `none`; violation=false ⇒ `none` |
| reason | string | kısa iz; yüzeye ÇIKMAZ, yalnız Review alanına yazılır |

- Kapalı enum FR-011 kapsamıyla birebir (küfür/hakaret/kişisel saldırı). Eleştirel ama küfürsüz
  olumsuz yorum İHLAL DEĞİLDİR — prompt bunu açıkça söyler ("ürün kötü" serbest).
- Şema dışı/parse edilemeyen yanıt = denetim başarısız ⇒ retry yolu (aşağıda); ASLA ihlal sayılmaz.

## Uygulama sınırı (İlke II)

- Agent yalnız KARAR verir. Gizlemeyi `Review.ApplyModeration(ModerationVerdict, now)` uygular:
  violation ⇒ `Status=Hidden` + kategori/gerekçe; temiz ⇒ yalnız `ModeratedAtUtc` damgası.
  Idempotent: damga doluysa no-op Ok (at-least-once teslimata dayanır).
- `ModerationVerdict.Create` guard: violation=true iken category boş/`none` olamaz.

## Kuyruk + dayanıklılık (FR-012, fail-open)

- Lokal durable kuyruk `reviews.moderate` (Wolverine, `procurement.enrich` emsali).
  Mesaj: `ModerateReviewCommand(ReviewId)` — metin DB'den okunur (bayat kopya taşınmaz).
- Retry 10s/30s/60s → error queue. Denetim beklerken/başarısızken yorum GÖRÜNÜR kalır.
- `ModerationOptions` (Options pattern): `OpenAI:ApiKey`+`Model` zorunlu, açılışta fail-fast.