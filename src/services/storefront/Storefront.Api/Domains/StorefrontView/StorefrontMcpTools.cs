namespace Storefront.Api.Domains.StorefrontView;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir (005 karari).
// 069: search_storefront_products + find_similar_books TAM IKAME ile silindi — tek kapi query_storefront.
// Aciklama dis agent'lar icin de sozlesmedir (Claude Desktop vb. yalniz bunu gorur): sema + kurallar burada.
[McpServerToolType]
public static class QueryStorefrontMcpTool
{
    [McpServerTool(Name = Shared.StorefrontTools.QueryStorefront)]
    [Description("Kitap magazasi vitrininde SERBEST salt-okur SQL sorgusu calistirir (Postgres). " +
                 "TEK ilişki: storefront_sellable (yalniz satistaki kitaplar). Kolonlar: " +
                 "product_id uuid, name text, description text, authors text[] (yazar adlari; " +
                 "unnest/ILIKE ile ara), publisher text, category text, price numeric, stock int " +
                 "(NULL=bilinmiyor), rating_average numeric (NULL=puansiz), rating_count int, " +
                 "specs jsonb ([{Attribute,Option}] ozellik ciftleri), family_code text (varyant " +
                 "ailesi), image_url text, added_at timestamptz (YAKLASIK eklenis), embedding vector " +
                 "(anlamsal temsil; yanita donmez). Kurallar: tek SELECT/WITH; baska iliski/yazma " +
                 "yasak; sonuc 50 satirla sinirlanir (truncated=true ise LIMIT/OFFSET ile sayfala). " +
                 "Anlamsal/temali arama icin metni {{EMBED:\"tema metni\"}} yer-tutucusuyla yaz " +
                 "(vektore sistem cevirir), or: embedding <=> {{EMBED:\"kis temali bilim kurgu\"}} < 0.68 " +
                 "AND embedding IS NOT NULL, ayni ifadeyle ORDER BY. Benzerlik: embedding <=> (SELECT " +
                 "embedding FROM storefront_sellable WHERE product_id = 'X') + product_id <> 'X'. " +
                 "Hata donerse (messages[].code) sorguyu duzeltip yeniden dene.")]
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
