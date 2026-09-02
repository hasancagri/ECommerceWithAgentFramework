# Library — Domain Süreci

**BC ne yapar:** Kullanıcının ürünle kalıcı ilgi kayıtlarını tutar; ilk dilim fiyat alarmı —
yaşayan abonelik olarak kurulur, her fiyat değişiminde bildirim tetiği yayınlar, gönderim izini saklar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Kullanıcı ürüne fiyat alarmı kurar.** E-posta kuruluş anında      `(PriceAlarm.Create)`
   snapshot alınır (doğrulanmaz, boş olabilir); ürün adı + o anki
   fiyat bağlam olarak saklanır.
2. **Aynı kullanıcı × ürüne tek alarm.** Mevcut kayıt varsa ikinci     `(CreatePriceAlarmCommandHandler)`
   kurma isteği idempotent başarı döner, yeni kayıt yazılmaz.
3. **Fiyat değişimi dinlenir.** Catalog'un ürün event'inde eski        `(LibraryEventHandlers ← ProductChangedEvent)`
   fiyat doluysa VE yeni fiyattan farklıysa tetik doğar; yön bakılmaz.
4. **Üründeki HER alarm için bildirim tetiği yayınlanır.** Mail'e      `(→ PriceAlarmTriggered)`
   yetecek her alan event'te (email snapshot dahil); worker kimseye sormaz.
5. **Alarm tetikte KAPANMAZ.** Yaşayan abonelik: kullanıcı kaldırana   `(PriceAlarm — mutator'suz)`
   dek her fiyat değişimi yeni tetik üretir.
6. **Kullanıcı alarmı kaldırır.** Hard delete; yaşam döngüsü biter.    `(RemovePriceAlarmCommandHandler)`
7. **Gönderim sonucu iz olarak yazılır.** Worker'ın sonucu             `(LibraryEventHandlers ← NotificationSent`
   append-only kayda düşer (sent / no-email / hata özeti).              ` → NotificationRecord)`

## Domain kuralları (süreci yöneten değişmezler)

- **E-posta snapshot'tır.** Kuruluş anındaki cookie claim değeri; sonradan değişen e-posta alarmı etkilemez.
- **Tetik = gerçek fiyat değişimi.** Eski fiyat yoksa (fiyat-dışı düzenleme) tetik yok; alarm yoksa sessiz.
- **Tek alarm kuralı idempotent.** İkinci kurma isteği hata değil, başarı (düğme durumu bozulmaz).
- **İz append-only.** `NotificationRecord` davranışsız dokümandır; alarm silinse de iz kalır.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat gerçeği Catalog'da; mail üretimi/gönderimi `notification-agent` worker'ında
(iletişim yalnız event: `PriceAlarmTriggered` / `NotificationSent`). SMTP/LLM bu BC'de YOK.