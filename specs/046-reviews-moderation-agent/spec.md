# Feature Specification: Reviews Moderasyon Agent'ını Ayrı Broker-Tabanlı Worker'a Taşı

**Feature Branch**: `046-reviews-moderation-agent`

**Created**: 2026-08-22

**Status**: Draft

**Input**: User description: "Reviews moderasyon agent'ını Reviews.Api BC'sinden ayrı bir broker-tabanlı worker servisine taşı; agent-framework kodu Reviews'te yazılı olmasın, iletişim MessageBroker üzerinden, fail-open, purchase-check değişmez."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Agent kodu yalnız agents/ altında (Priority: P1)

Bakımcı olarak, yorum moderasyonunun LLM kodunun (agent-framework) yalnız `src/agents/`
altındaki bir projede yaşamasını isterim; Reviews.Api BC'sinde agent-framework kodu bulunmasın.
Böylece "agent yazımı" tek yerde toplanır, BC domain'iyle karışmaz.

**Why this priority**: Feature'ın asıl amacı budur; diğer her şey bu izolasyonu korurken sağlanır.

**Independent Test**: Reviews.Api kaynağında `Microsoft.Agents.AI`/`ChatClientAgent`/OpenAI referansı
kalmadığı doğrulanır; moderasyon LLM çağrısı ayrı bir process'te koşar; çözüm derlenir.

**Acceptance Scenarios**:

1. **Given** taşıma tamam, **When** Reviews.Api kaynağı taranır, **Then** hiçbir agent-framework/OpenAI kodu bulunmaz.
2. **Given** sistem Aspire'dan açık, **When** bir yorum moderasyona uğrar, **Then** karar ayrı worker process'inde üretilir.

---

### User Story 2 - Yorumcu deneyimi korunur (Priority: P1)

Satın almış kullanıcı olarak, ürüne yorum yazdığımda yorumum anında görünür; sakıncalıysa
(küfür/hakaret/kişisel saldırı) kısa süre sonra gizlenir. Bu davranış taşımadan sonra değişmez.

**Why this priority**: Uçtan uca son-kullanıcı davranışı bozulmamalı; refactor gözlemlenebilir değişiklik üretmemeli.

**Independent Test**: Temiz yorum Visible kalır; sakıncalı yorum async denetimden sonra Hidden olur;
gizlenince ürün özeti (ortalama/sayı) yeniden hesaplanıp Storefront'a yansır.

**Acceptance Scenarios**:

1. **Given** satın alma kanıtı var, **When** temiz metinli yorum gönderilir, **Then** yorum Visible doğar ve Visible kalır.
2. **Given** yorum sakıncalı metin içerir, **When** async moderasyon tamamlanır, **Then** yorum Hidden olur ve özet güncellenir.
3. **Given** ürüne sert ama küfürsüz olumsuz yorum, **When** moderasyon koşar, **Then** ihlal sayılmaz, Visible kalır.

---

### User Story 3 - Broker/agent kesintisinde dayanıklılık (Priority: P2)

Kullanıcı olarak, mesaj broker'ı veya moderasyon worker'ı geçici olarak down olsa bile yorum
gönderimim başarısız olmaz; yorum görünür kalır, moderasyon altyapı dönünce geç de olsa çalışır.

**Why this priority**: Moderasyon kritik değildir; kesinti son-kullanıcı akışını kesmemeli (fail-open).

**Independent Test**: Broker down iken yorum gönderilir → submit reviewsDb'ye commit olur, kullanıcı
başarı görür; broker döndüğünde bekleyen moderasyon isteği relay edilip işlenir.

**Acceptance Scenarios**:

1. **Given** broker erişilemez, **When** yorum gönderilir, **Then** submit başarılı olur ve yorum Visible görünür.
2. **Given** worker down, **When** yorumlar gönderilir, **Then** hiçbiri kaybolmaz; worker dönünce hepsi denetlenir.
3. **Given** broker uzun süre down, **When** submit yapılır, **Then** yorum yine görünür; moderasyon yalnız gecikir (kayıp kabul, kritik değil).

---

### Edge Cases

- **Metinsiz yorum (yalnız yıldız)**: moderasyon isteği hiç yayınlanmaz (denetlenecek içerik yok); yorum Visible kalır.
- **En-az-bir-kez teslim**: aynı moderasyon sonucu iki kez gelirse ikinci uygulama no-op (zaten denetlenmiş).
- **Şema-dışı LLM çıktısı**: kapalı kategori kümesine uymayan karar hata sayılır → retry → error queue; yorum Visible kalır.
- **Silinmiş/bilinmeyen ReviewId**: sonuç gelince sessiz no-op.
- **Worker geç dönüş**: yorum uzun süre denetlenmemiş Visible kalabilir; sonradan Hidden olabilir (kabul).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Moderasyon LLM çağrısı Reviews.Api process'inde DEĞİL, `src/agents/` altındaki ayrı bir worker servisinde koşmalıdır.
- **FR-002**: Reviews.Api kaynak kodu agent-framework (Microsoft.Agents.AI/ChatClientAgent) ve OpenAI referansı İÇERMEMELİDİR.
- **FR-003**: Reviews ile moderasyon worker'ı arasındaki iletişim yalnız mesaj broker'ı (async event) üzerinden olmalıdır; senkron çağrı olmamalıdır.
- **FR-004**: Yorum gönderim yolu (submit) broker/worker'a SENKRON bağımlı olmamalı; yalnız kendi veritabanına yazıp başarı dönmelidir.
- **FR-005**: Broker down iken gönderilen moderasyon isteği kaybolmamalı; broker dönünce dayanıklı biçimde teslim edilmelidir (transactional outbox).
- **FR-006**: Yorum gönderildiğinde anında Visible doğmalı (post-moderation); moderasyon bir ön-yayın kapısı OLMAMALIDIR.
- **FR-007**: Moderasyon "ihlal" kararı verirse yorum Hidden yapılmalı; temizse Visible kalmalıdır.
- **FR-008**: İhlal nedeniyle bir yorum gizlenince ürün yorum özeti (ortalama + sayı) yeniden hesaplanıp Storefront'a yayınlanmalıdır.
- **FR-009**: Moderasyon isteği PII taşımamalıdır: yalnız yorum metni + yıldız puanı + yorum kimliği gönderilir (kullanıcı adı/Id ASLA).
- **FR-010**: Metinsiz (yalnız yıldız) yorum için moderasyon isteği yayınlanmamalıdır.
- **FR-011**: Moderasyon sonucunun uygulanması en-az-bir-kez teslime dayanıklı (idempotent) olmalı; zaten denetlenmiş yorum tekrar gelirse no-op.
- **FR-012**: Worker moderasyon kararını sabit, kapalı bir kategori kümesiyle vermelidir: profanity, insult, personal_attack, none.
- **FR-013**: Ürüne/markaya sert eleştiri ihlal SAYILMAZ; yalnız kişiye (satıcı/kullanıcı/çalışan) yönelik küfür/hakaret/saldırı ihlaldir.
- **FR-014**: Worker'da LLM çağrısı başarısızsa yeniden denenmeli (kademeli), tükenince error queue'ya düşmeli; bu dayanıklılık worker'da yaşamalıdır.
- **FR-015**: Broker binding'ini tüketen taraf kurmalıdır (yayıncı yalnız exchange deklare eder — soğuk-açılış dersi).
- **FR-016**: Satın-alma-kanıtı kontrolü (Order senkron, fail-closed) DEĞİŞMEMELİDİR; kapsam dışıdır.
- **FR-017**: Reviews aggregate davranışı, reviewsDb şeması, eligibility/query'ler ve ReviewSummaryChanged→Storefront event'i DEĞİŞMEMELİDİR.

### Key Entities *(include if feature involves data)*

- **ReviewModerationRequested (event)**: Reviews'ten worker'a moderasyon isteği; alanlar: yorum kimliği, yorum metni, yıldız puanı. PII yok.
- **ReviewModerated (event)**: Worker'dan Reviews'e karar; alanlar: yorum kimliği, ihlal (bool), kategori, kısa gerekçe.
- **Moderasyon Worker Servisi**: `src/agents/` altında, kendi DB'si olmayan, broker tüketen/yayınlayan, LLM moderasyon kararı üreten ayrı process.
- **Review (mevcut aggregate)**: Değişmez; yalnız moderasyon kararı sonucu Visible/Hidden durumu güncellenir (mevcut ApplyModeration).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Reviews.Api kaynağında agent-framework/OpenAI referansı sayısı = 0.
- **SC-002**: Moderasyon LLM çağrılarının %100'ü Reviews.Api dışındaki ayrı worker process'inde gerçekleşir.
- **SC-003**: Broker down iken yapılan yorum gönderimlerinin %100'ü başarılı olur ve yorum görünür (submit kaybı = 0).
- **SC-004**: Sakıncalı yorumlar (canlı smoke) worker denetiminden sonra gizlenir; temiz ve ürün-eleştiren yorumlar görünür kalır.
- **SC-005**: Taşımadan sonra Reviews.Api.Tests tümü geçer; çözüm 0 hata ile derlenir; son-kullanıcı gözlemlenebilir davranışı değişmez.

## Assumptions

- Moderasyon kritik-olmayan bir arka-plan adımıdır; uzun broker kesintisinde geç/atlanan moderasyon kabul edilir (fail-open).
- Mevcut Wolverine + Marten transactional outbox altyapısı submit yolunda broker-dayanıklılığı sağlar (yeni altyapı gerekmez).
- Worker servisi kendi OpenAI gizli anahtarını alır (fail-fast); Reviews'in OpenAI bağımlılığı kalkar.
- Moderasyon worker'ı durumsuzdur (DB yok); tek kaynak Reviews'in reviewsDb'sidir.
- Repo'nun RabbitMQ fanout + "tüketici binding kurar" deseni bu iki yeni event için aynen kullanılır.
- Değişiklik yalnız moderasyon adımını taşır; satın-alma-kanıtı, özet hesaplama ve Storefront yayını mantığı korunur.
