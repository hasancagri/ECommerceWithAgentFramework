namespace ProductEnrichmentAgent;

public static class McpServers
{
    public const string Catalog = "catalog";
    public const string File = "file";
}

public static class CatalogTools
{
    public const string ListIncompleteProducts = "list_incomplete_products";
    public const string SetProductDescription = "set_product_description";
    public const string SetProductImage = "set_product_image";
}

public static class FileTools
{
    public const string UploadProductImage = "upload_product_image";
}

public static class AgentConstants
{
    // MCP cagrilari icin token enjekte eden named HttpClient'in anahtari (kod sembolu, ayar degil).
    public const string McpHttpClient = "mcp";
}

public static class Prompts
{
    // FR-002: alakali, bos-olmayan, en fazla 100 karakter aciklama. AI 100 sinirini garanti eder.
    public const string Description =
        """
        Sen bir e-ticaret icerik yazarisin. Sana bir urunun adi ve markasi verilir.
        Bu urun icin TEK bir satirlik, alakali ve gercekci bir Turkce urun aciklamasi uret.
        Kurallar:
        - En fazla 100 karakter (bosluklar dahil). Bu siniri ASLA asma.
        - Yalnizca aciklama metnini dondur; tirnak, baslik, on-ek veya aciklama ekleme.
        - Generic/placeholder ("harika urun", "aciklama yok") yazma; urune ozgu, somut ol.
        - Urun adi/markasi anlamli bir aciklama uretmeye yetmiyorsa bos string dondur.
        """;

    // Image agent'in ic adimi: gercek gorsel uretimi icin OpenAI image API'ye verilecek prompt.
    public const string ImagePrompt =
        """
        Sen bir gorsel prompt uzmanisin. Sana bir urunun adi ve markasi verilir.
        Bu urunun tek basina, duz/beyaz stüdyo arka planinda, urun-fotografi tarzinda,
        gercekci bir katalog gorselini uretecek KISA bir Ingilizce image-generation prompt yaz.
        Kurallar:
        - Yalnizca prompt metnini dondur; aciklama/etiket ekleme.
        - Metin, logo, filigran veya insan olmasin; yalnizca urunun kendisi.
        - Urun tanimlanamiyorsa bos string dondur.
        """;
}