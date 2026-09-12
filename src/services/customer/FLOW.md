# Customer — Domain Süreci

**BC ne yapar:** Kullanıcının **cüzdanını** (ince: PG kart-handle çapası + varsayılan kart tercihi; kart
verisi PG'de, PAN yok) ve **adres defterini** tutar. Chat/checkout yolunun okuduğu kayıtlı ödeme/teslimat
kaynağıdır. İzole BC: hiçbir integration event yayınlamaz/tüketmez; kanalı MCP (chat) + yapısal S2S REST
(Order/Payment) + PG hosted-kart callback'i (JWT'siz).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-invariant) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Adres eklenir/güncellenir/silinir + varsayılan seçilir.**       `(AddressBook.AddAddress`
   ≤1 varsayılan invariant'ı defterde tek yazmada korunur;           ` / SetDefaultAddress)`
   yüzey chat/MCP.
2. **Kart ekleme başlatılır (hosted link).** PG'den hosted form      `(StartAddCardForAgent`
   linki + tek-kullanımlık oturum alınır; PAN mağazaya uğramaz.      ` → IPgCardClient)`
3. **Kart ekleme tamamlanır (callback).** PG callback'i oturumu      `(CompleteAddCardForAgent`
   UserId'ye çözer; PG kullanıcı-handle çapası yazılır, ilk kart     ` → Wallet.SetPgUserHandle`
   varsayılan olur (FR-001a).                                        ` / MarkFirstCardDefault)`
4. **Kayıtlı kartlar okunur (canlı, PG'den).** Wallet.PgUserHandle   `(GetCardsForAgent`
   → PG list-cards → gösterilebilir alanlar + opak cardHandle;       ` → IPgCardClient)`
   PAN/CVV/token asla. Cache yok.
5. **Kart silinir / varsayılan yapılır.** Sahiplik PG listesinde     `(DeleteCardForAgent`
   doğrulanır; sil→varsayılansa temizlenir (FR-012); varsayılan      ` / SetDefaultCardForAgent`
   tercihi mağazada tutulur.                                         ` → Wallet.SetDefaultCard)`
6. **Sipariş/çekim ödeme bağlamını yapısal kanaldan çeker.**         `(GetPaymentContextForAgent)`
   PgUserHandle + CardHandle + buyer + varsayılan adres; Order
   (adres ön-kontrolü) + Payment (NON-3D çekim) makine token'ıyla okur.

## Domain kuralları (süreci yöneten değişmezler)

- **PAN/CVV/kart verisi mağazada YOK.** Kart PG/sağlayıcıda yaşar; mağaza yalnız opak PG handle'ları (kullanıcı-handle + kart-handle) tutar.
- **PG kullanıcı-handle idempotent.** `Wallet.SetPgUserHandle` bir kez yazar; sonraki eklemeler aynı handle'ı korur (aynı kullanıcı = aynı küme).
- **En fazla 1 varsayılan.** `Wallet.DefaultCardHandle` tek varsayılan; `AddressBook`'ta da varsayılan seçimi diğerlerini atomik temizler.
- **Sahiplik PG listesi üzerinden.** Sil/varsayılan yalnız çağıranın PgUserHandle'ının PG kartlarında geçerli (FR-008 fail-closed).
- **Kullanıcı başına tek cüzdan/defter.** `UserId` ile keyli; ilk yazımda tembel oluşturulur.
- **İzole BC, event yok.** Kanal REST/MCP + PG hosted-kart callback (JWT'siz, tek-kullanımlık oturum korelasyonu).

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim Payment BC'nin işi (NON-3D, PG'ye; onay agent konuşmasında — FR-014), sipariş Order BC'nin.
Kart verisi (PAN/CVV) mağaza sınırının ötesinde (PG/sağlayıcı) — mağaza yalnız hosted link + opak handle görür.
