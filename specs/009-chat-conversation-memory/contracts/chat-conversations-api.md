# Contracts: Konuşma Uçları (ChatAgent + WebApp BFF)

## ChatAgent — `my-conversations` (yeni; JWT bearer, sahiplik `sub` ile)

### POST /v1/my-conversations  (AllowAnonymous — anonim de sohbet açar)

- Body: `{ "agentName": "public" | "assistant" }`
- 200: `{ "conversationId": "conv_..." }` — login'de OwnerUserId token'dan yazılır, anonimde null.
- Not: Framework'ün `POST /v1/conversations` ucu map edilmez (R5).

### GET /v1/my-conversations?page=1&pageSize=20  (auth zorunlu)

- 200: `{ "items": [{ "conversationId", "title", "agentName", "lastActivityTime" }], "page", "pageSize", "totalCount" }`
- Sıralama: lastActivityTime desc. Yalnız çağıranın konuşmaları.

### GET /v1/my-conversations/{id}/items?page=1&pageSize=100  (auth zorunlu)

- 200: `{ "items": [{ "sequence", "role", "text", "kind", "createdTime" }], ... }` — UI için
  sadeleştirilmiş görünüm (kind: message | tool). TAM geçmiş, kırpma yok (FR-004).
- 404: konuşma yok YA DA çağıranın değil (varlık sızdırılmaz, FR-007).

## ChatAgent — `POST /v1/chat` (PİVOT: widget'ın sohbet ucu; Responses uçları widget yolundan çıktı)

- Body: `{ "conversationId": "conv_...", "message": "..." }`. Agent, konuşmanın kaydındaki agent'tır.
- Sahiplik: login konuşması yalnız sahibine; sahipsiz (anonim) konuşma id bilene. Yok/başkasının → 404.
- SSE akışı: `data: {"delta":"..."}` metin parçaları; kapanış `data: {"done":true,"conversationId":"..."}`.
  `done` yazılmadan önce kullanıcı+asistan+araç mesajları kalıcılaşmıştır.
- Model input'u: konuşmanın son N item'ı (config; depo tam kalır, FR-005).
- `previous_response_id` akışı kullanılmaz; hosted `/…/v1/responses` uçları widget tarafından çağrılmaz.

## WebApp BFF — `/chat/*` (widget'ın tek kapısı)

- `POST /chat/conversations` → ChatAgent create'e vekil; auth durumuna göre agent seçer,
  token ekler. 200: `{ "conversationId" }`.
- `GET /chat/conversations` (yalnız login) → liste vekili.
- `GET /chat/conversations/{id}` (yalnız login) → item vekili.
- `POST /chat/stream` → body `{ "message", "conversationId" }` (PreviousResponseId kalkar);
  ChatAgent `/v1/chat`'e vekildir, SSE pass-through. Id yoksa/404 ise BFF yeni conversation açıp
  isteği tekrarlar; kullanılan id her yanıtta `X-Conversation-Id` header'ıyla bildirilir.

## Widget sözleşmesi

- Conversation id `sessionStorage`'da (`chat.conversationId`); "yeni sohbet" id'yi siler.
- Login panelde liste + geçmiş açma; anonimde liste yok (yalnız süreklilik).