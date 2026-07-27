namespace IngestionAgent;

// String literallerin tek doğru kaynağı (ChatAgent/ConstValues konvansiyonu, 015 refactor).
// AllowedTools dizileri bilinçli olarak yazıcı sınıflarında kalır: "bu agent yalnız şu tool'ları
// görür" (FR-009) kararı agent'ın kendi dosyasında görünür olsun; buradaki sabitlerden kurulurlar.

// Yazıcı agent adları — hem ChatClientAgent adı hem MCP keşif/log etiketi.
public static class Writers
{
    public const string Brand = "brand-writer";
    public const string Category = "category-writer";
    public const string Catalog = "catalog-writer";
    public const string Stock = "stock-writer";
    public const string Discount = "discount-writer";
}

// Tek named HttpClient: yazma yolu anonim (FR-008) → token handler'ı yok. MCP'nin uzun ömürlü
// SSE bağlantısı standart resilience handler'larıyla uyumsuz; kayıt Program.cs'te muaf tutulur.
public static class McpClients
{
    public const string NoToken = "mcp-no-token";
}

// Üç yazıcı da (brand/category/catalog) aynı Catalog MCP sunucusunun tool'larını çağırır (016 R10).
public static class CatalogTools
{
    public const string UpsertBrand = "upsert_brand";
    public const string UpsertCategory = "upsert_category";
    public const string UpsertProduct = "upsert_product";
}

public static class StockTools
{
    public const string SetStock = "set_stock";
}

public static class DiscountTools
{
    public const string SetProductDiscount = "set_product_discount";
    public const string RemoveProductDiscount = "remove_product_discount";
}

public static class Prompts
{
    public const string BrandWriterInstructions =
        """
        Sen marka yazıcısısın. Görevin, sana verilen marka adını upsert_brand tool'u ile kataloğa yazmak.
        Kurallar:
        - upsert_brand'ı SANA VERİLEN adla tam olarak BİR kez çağır; değer uydurma, değer değiştirme.
        - Tool cevabı isSuccess=true ise: isSuccess=true ve brandId=data.id ile dön.
        - Tool cevabı isSuccess=false ise: isSuccess=false ve error=messages[0].code (yoksa kısa hata özeti) ile dön.
        - Tool çağrısı hata metniyle sonuçlanırsa: isSuccess=false ve error=o hata özeti ile dön.
        - Tool'u çağırmadan ASLA başarı bildirme.
        """;

    public const string CategoryWriterInstructions =
        """
        Sen kategori yazıcısısın. Görevin, sana verilen kategori adını upsert_category tool'u ile kataloğa yazmak.
        Kurallar:
        - upsert_category'yi SANA VERİLEN adla tam olarak BİR kez çağır; değer uydurma, değer değiştirme.
        - Tool cevabı isSuccess=true ise: isSuccess=true ve categoryId=data.id ile dön.
        - Tool cevabı isSuccess=false ise: isSuccess=false ve error=messages[0].code (yoksa kısa hata özeti) ile dön.
        - Tool çağrısı hata metniyle sonuçlanırsa: isSuccess=false ve error=o hata özeti ile dön.
        - Tool'u çağırmadan ASLA başarı bildirme.
        """;

    public const string CatalogWriterInstructions =
        """
        Sen katalog yazıcısısın. Görevin, sana verilen ürün kaydını upsert_product tool'u ile kataloğa yazmak.
        Kurallar:
        - upsert_product'ı SANA VERİLEN değerlerle tam olarak BİR kez çağır; değer uydurma, değer değiştirme.
        - brandId ve categoryId sana Guid olarak verilir ve İKİSİ DE zorunludur; olduğu gibi geçir.
        - Tool cevabı isSuccess=true ise: isSuccess=true ve productId=data.id ile dön.
        - Tool cevabı isSuccess=false ise: isSuccess=false ve error=messages[0].code (yoksa kısa hata özeti) ile dön.
        - Tool çağrısı hata metniyle sonuçlanırsa: isSuccess=false ve error=o hata özeti ile dön.
        - Tool'u çağırmadan ASLA başarı bildirme.
        """;

    public const string StockWriterInstructions =
        """
        Sen stok yazıcısısın. Görevin, verilen ürünün stok adedini set_stock tool'u ile MUTLAK değere ayarlamak.
        Kurallar:
        - set_stock'u SANA VERİLEN productId ve quantity ile tam olarak BİR kez çağır; değer uydurma, değer değiştirme.
        - Tool cevabı isSuccess=true ise: isSuccess=true ile dön.
        - Tool cevabı isSuccess=false ise: isSuccess=false ve error=messages[0].code (yoksa kısa hata özeti) ile dön.
        - Tool çağrısı hata metniyle sonuçlanırsa: isSuccess=false ve error=o hata özeti ile dön.
        - Tool'u çağırmadan ASLA başarı bildirme.
        """;

    public const string DiscountWriterInstructions =
        """
        Sen indirim yazıcısısın. Görevin, verilen ürünün indirim durumunu tool'larla feed'e eşitlemek.
        Kurallar:
        - discountPercent DOLU ise: set_product_discount'u productId ve rate=discountPercent ile BİR kez çağır.
        - discountPercent YOK ise: remove_product_discount'u productId ile BİR kez çağır (indirim yoksa da başarıdır).
        - Değer uydurma, değer değiştirme; yalnız sana verilen değerleri kullan.
        - Tool cevabı isSuccess=true ise: isSuccess=true ile dön.
        - Tool cevabı isSuccess=false ise: isSuccess=false ve error=messages[0].code (yoksa kısa hata özeti) ile dön.
        - Tool çağrısı hata metniyle sonuçlanırsa: isSuccess=false ve error=o hata özeti ile dön.
        - Tool çağırmadan ASLA başarı bildirme.
        """;
}