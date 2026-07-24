# Data Model: ChatAgent Kalıcı Konuşma Memory'si

Depo: `chatAgentDb`, Marten dokümanları (ChatAgent şeması). Domain aggregate DEĞİL — iş kuralı
taşımayan altyapı kayıtları (plan Complexity #1). Serileştirme repo standardı (Newtonsoft).

## ConversationDocument

| Alan | Tip | Not |
|------|-----|-----|
| Id | string | Conversation id (`conv_...`); Marten identity |
| AgentName | string | `public` / `assistant` — hangi agent'la |
| OwnerUserId | string? | Login kullanıcının `sub`'ı; anonimde null |
| Title | string | İlk kullanıcı mesajından türetilir (ConversationRules.DeriveTitle) |
| CreatedTime | DateTimeOffset | |
| LastActivityTime | DateTimeOffset | Her item eklenişinde güncellenir; liste sıralaması + TTL bunu okur |

**Kurallar**: Title boş mesajda "Yeni sohbet"; maksimum ~60 karakter, kelime sınırında kırpılır.
Liste sorgusu: OwnerUserId eşit + LastActivityTime desc + sayfalama. TTL seçimi:
OwnerUserId null AND LastActivityTime < now - AnonymousTtlHours.

## ConversationItemDocument

| Alan | Tip | Not |
|------|-----|-----|
| Id | string | Item id (`item_...` üretimi bizde); Marten identity |
| ConversationId | string | FK (mantıksal); indexli |
| Sequence | long | Konuşma içi sıra; ekleme sırasında atanır (monoton artan) |
| ItemJson | string | MAF `ItemResource` serileştirilmiş hali — mesaj/araç çağrısı/araç sonucu |
| CreatedTime | DateTimeOffset | |

**Kurallar**: Item'lar immutable'dır (update yok; conversation silinince toplu silinir).
Model input penceresi: ConversationId eşit, Sequence desc, ilk N (config `Chat:ContextWindowItems`,
varsayılan 40), sonra kronolojik sıraya çevrilir. UI okuma: Sequence asc, tamamı (sayfalı).

## İlişkiler ve durum geçişleri

- Conversation 1—N Item. Silme: yalnız TTL süpürücüsü (anonim) — login konuşması silinmez (FR-008).
- Durum makinesi yok; LastActivityTime dışında conversation güncellenmez.

## Doğrulama (FR eşlemesi)

- FR-001/007: OwnerUserId sahiplik süzgeci her sorguda zorunlu (endpoint katmanında).
- FR-004 vs FR-005: iki okuma yolu — UI tam (Sequence asc), model pencereli (son N).
- FR-006: araç çağrıları da ItemJson olarak aynı tabloda.
- FR-009: TTL yalnız OwnerUserId null olanlara dokunur.