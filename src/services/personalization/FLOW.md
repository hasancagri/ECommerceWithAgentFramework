# Personalization — Domain Süreci

**BC ne yapar:** WebApp'in yazdığı davranış günlüğünü (sürümlü JSONL) kendi DB'sine aktarır, bu
etkileşimlerden ALS modeli eğitir ve kişisel öneri listesini servis eder. .NET buraya doğrudan bağlanmaz.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **WebApp davranışı sürümlü JSONL'e yazar** (telemetri-kanalı istisnası).       `(behavior-*.jsonl)`
   `schemaVersion=1` şeması sözleşmedir; BC dosyayı okur, WebApp'e bağlanmaz.
2. **Günlük periyodik olarak DB'ye aktarılır.** Her dosya offset-takipli,          `(ingest_once)`
   yalnız TAMAMLANMIŞ satırlar okunur; yarım satır sonraki tura kalır.             `(read_new_lines)`
3. **Her satır doğrulanıp iç modele çevrilir.** Şema/tip uyuşmayan satır          `(parse_line)`
   sessizce ATLANIR; bozuk tek satır turu kesmez.
4. **Aktarım idempotenttir.** `(source_file, line_no)` tekilliği + offset TEK       `(ingest_once)`
   transaction; offset kaybolsa bile çift kayıt engellenir.
5. **Etkileşimler user×item matrise dönüşür.** Kimlik = user varsa user,           `(build_matrix)`
   yoksa anonim; ProductViewed=1, BasketItemAdded=3 ağırlıklı (ListShown girmez).
6. **ALS modeli periyodik eğitilir + atomik yazılır.** tmp + rename;               `(fit_als → save_model)`
   okuyucu yarım dosya görmez. Aynı turda son-7-gün popüler yenilenir.             `(refresh_popular)`
7. **En son başarılı model kilitli SWAP ile devreye alınır.** Eğitim              `(ModelStore.reload)`
   sırasında sorgu kesintisi yok; tek referans atomik değişir.                     `(AlsModel)`
8. **Öneri zinciri asla boş dönmez.** personal → session → popular:               `(recommend)`
   kullanıcı modelde yoksa oturum ürünlerinden, o da yoksa popülerden.

## Domain kuralları (süreci yöneten değişmezler)

- **Yazım yolu = davranış günlüğü.** BC yalnız sürümlü JSONL'den beslenir; .NET DB'sine/API'sine bağlanmaz.
- **Bozuk satır turu kesmez (FR-010).** Doğrulanamayan satır atlanır + sayılır; ingest devam eder.
- **Kimlik önceliği eğitim ve okumada AYNI.** user_id varsa o, yoksa anonymous_id — iki yolda tutarlı.
- **Öneri fallback zorunlu (FR-012/013).** Model/kullanıcı yoksa oturum, o da yoksa popüler; boş liste yok.
- **Model swap kilitli, eğitim okumayı bloklamaz (FR-011).** Yeni model tek referans atomik takas edilir.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat/stok yok — yalnız ProductId önerir; ürün detayını okuyan WebApp'tir. Model eğitimi
DB'sindeki davranış geçmişiyle sınırlı; canlı olay akışı/gerçek-zaman skorlama kapsam dışı.
