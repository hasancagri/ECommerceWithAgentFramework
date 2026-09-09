namespace Storefront.Api.Domains.StorefrontView;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir (005 karari).
// 069: search_storefront_products + find_similar_books TAM IKAME ile silindi — tek kapi query_storefront.
// 070 US5/FR-013: 069 sorgu rehberinin (playbook) KANONIK EVI artik bu Description — dis agent'lar
// (Claude Desktop vb.) rehberi tool'un kendisinden ogrenir; ChatAgent prompt kopyasi DONDURULDU
// (071 sokumune dek paralel yasar, oraya dokunma). ChatAgent-ozgu persona satirlari (kural 8/9,
// sepete ekleme) BURAYA GIRMEZ — yalniz SORGU rehberi (R7). Sema drift guard'i bu dosyayi hedefler
// (check-agent-query-schema.sh); kolon adlari duz metin kalmali.
[McpServerToolType]
public static class QueryStorefrontMcpTool
{
    // Rehber ayri const'ta: Description attribute'u derleme sabiti ister; bloklar okunur kalsin.
    private const string SchemaBlock =
        "Kitap magazasi vitrininde SERBEST salt-okur SQL sorgusu calistirir (Postgres). " +
        "TEK ilişki: storefront_sellable (yalniz satistaki kitaplar). Kolonlar: " +
        "product_id uuid, name text (kitap adi), description text (aciklama), authors text[] " +
        "(yazar adlari), publisher text (yayinevi), category text (kategori), price numeric (TL), " +
        "stock int (NULL=bilinmiyor), rating_average numeric (NULL=hic puan yok), rating_count int, " +
        "specs jsonb ([{Attribute,Option}] ozellik ciftleri), family_code text (varyant ailesi; " +
        "NULL=ailesiz), image_url text (kapak), added_at timestamptz (YAKLASIK eklenis), " +
        "embedding vector (anlamsal temsil; yanita donmez, yalniz <=> mesafesinde kullan). " +
        "KURALLAR: tek SELECT/WITH; baska iliski/yazma YASAK; sonuc 50 satirla sinirlanir. ";

    private const string PatternsBlock =
        "SORGU KALIPLARI: " +
        "(1) KATALOG INGILIZCE: kategori/tur adlari Ingilizcedir — kullanici Turkce soylerse " +
        "Ingilizce karsiligiyla ara ('kurgu/roman' → '%fiction%', 'fantastik' → '%fantasy%', " +
        "'bilim kurgu' → '%science%'); Turkce kelimeyle ILIKE aramasi BOS doner. " +
        "(2) Ad/kelime: name ILIKE '%dune%'. Yazar: EXISTS (SELECT 1 FROM unnest(authors) a WHERE " +
        "a ILIKE '%le guin%') — TAM AD YAZMA, en ayirt edici parcayi (genelde soyad) yaz. " +
        "Yayinevi/kategori de ILIKE ile. " +
        "(3) Ozellik/varyant: EXISTS (SELECT 1 FROM jsonb_array_elements(specs) s WHERE " +
        "s->>'Attribute' ILIKE '%cilt%' AND s->>'Option' ILIKE '%ciltli%'). 'Bunun ciltli hali var " +
        "mi' icin: family_code = (SELECT family_code FROM storefront_sellable WHERE product_id = 'X') " +
        "AND product_id <> 'X'. " +
        "(4) Istatistik/karsilastirma/uc deger: GROUP BY + COUNT/AVG/MIN/MAX, ORDER BY + LIMIT; " +
        "VEYA/HARIC tek sorguda OR/NOT ile — elle cok arama yapip birlestirme. " +
        "(5) Puan sarti: rating_average > 4 (NULL'lar kendiliginden elenir). " +
        "(6) SAYFALAMA/genis liste: 'tum X'leri listele' isteginde ONCE COUNT(*) ile toplami ogren, " +
        "sonra ilk sayfayi LIMIT 20 ile ver; toplami soyle ve devamini isteyip istemedigini sor — " +
        "devami = AYNI sorgu, sonraki OFFSET. Yanitta truncated=true ise ayni davranis (sonuc " +
        "kirpilmistir; 'hepsini gosterdim' deme). ";

    private const string SemanticBlock =
        "TEMALI/ANLAMSAL arama: bulanik tema/ruh hali/konu ifadesini {{EMBED:\"tema metni\"}} " +
        "yer-tutucusuyla yaz (vektore sistem cevirir; icine fiyat/yazar gibi yapisal kisim YAZMA). " +
        "Kalip: WHERE embedding IS NOT NULL AND embedding <=> {{EMBED:\"kis temali surukleyici " +
        "bilim kurgu\"}} < 0.68 ORDER BY embedding <=> {{EMBED:\"kis temali surukleyici bilim " +
        "kurgu\"}} — yapisal kisitlar (fiyat/stok/kategori/haric) AYNI sorguda WHERE'e eklenir. " +
        "0.68 ustu mesafe ALAKASIZDIR; esigi asla gevsetme. " +
        "BENZERLIK ('buna benzer ne var'): embedding kolonunu SELECT listesine ASLA YAZMA (degeri " +
        "donmez); once product_id bul, sonra TEK sorguda alt-sorgu kalibi (yeni {{EMBED}} URETME): " +
        "SELECT name, price FROM storefront_sellable WHERE product_id <> 'X' AND embedding IS NOT " +
        "NULL AND embedding <=> (SELECT embedding FROM storefront_sellable WHERE product_id = 'X') " +
        "< 0.68 ORDER BY embedding <=> (SELECT embedding FROM storefront_sellable WHERE " +
        "product_id = 'X') LIMIT 8. Istenirse fiyat/stok kisiti da eklenir. ";

    private const string HonestyBlock =
        "DUZELTME: hata donerse (messages[].code + property ipucu) sorguyu duzeltip EN FAZLA 2 kez " +
        "yeniden dene; yine olmazsa bu sorunun su an yanitlanamadigini soyle — teknik ayrinti dokme. " +
        "DURUST VERI SINIRI: satis adedi/bestseller verisi vitrinde YOK — 'en cok satan' sorulursa " +
        "bu verinin tutulmadigini durustce soyle, asla uydurma. added_at YAKLASIKTIR (kayit " +
        "guncellenme zamani) — eklenis sorularinda yaklasikligi belirt. " +
        "GROUNDING: yaniti YALNIZ donen satirlardan kur. Bos sonuc = durust 'bulunamadi' (hata " +
        "degildir); asla satir/alan/deger uydurma, alakasiz oneri sunma.";

    [McpServerTool(Name = Shared.StorefrontTools.QueryStorefront)]
    [Description(SchemaBlock + PatternsBlock + SemanticBlock + HonestyBlock)]
    public static Task<FeatureObjectResultModel<QueryStorefrontForAgent.QueryStorefrontResponse>> QueryStorefrontAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("storefront_sellable uzerinde TEK SELECT/WITH sorgusu; anlamsal metin {{EMBED:\"...\"}} ile")]
        string sql)
        => bus.InvokeAsync<FeatureObjectResultModel<QueryStorefrontForAgent.QueryStorefrontResponse>>(
            new QueryStorefrontForAgent.QueryStorefrontQuery(sql), ct);
}

// 067 NOT: kesif envanteri tool'lari (list_categories/authors/publishers) Catalog'dadir
// (envanter otoritesi = Catalog; Storefront = tek sorgu kapisi yuzeyi).