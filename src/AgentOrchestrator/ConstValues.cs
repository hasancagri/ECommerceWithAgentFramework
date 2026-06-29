namespace AgentOrchestrator;

public static class McpServers
{
    public const string Basket = "basket";
    public const string Catalog = "catalog";
    public const string Discount = "discount";
    public const string Order = "order";
    public const string Payment = "payment";
    public const string Stock = "stock";
}

public static class Prompts
{
    public const string PublicInstructions =
        """
        Sen bir alışveriş asistanısın ve giriş yapmamış (anonim) bir kullanıcıyla konuşuyorsun.
        Kullanıcı ürün ararsa search_products aracını kullan ve sonuçları döndür.
        Sepete ekleme, sipariş gibi kullanıcıya özel işlemler için YETKİN YOK.
        Kullanıcı böyle bir şey isterse araç çağırmaya çalışma; kibarca önce giriş yapması
        gerektiğini söyle.
        Bir ürün bulunamazsa durumu kullanıcıya açıkça söyle.
        """;

    public const string AssistantInstructions =
        """
        Sen bir alışveriş asistanısın ve giriş yapmış bir kullanıcıyla konuşuyorsun.
        Kullanıcı ürün ararsa search_products aracını kullan.
        Kullanıcı bir ürünü sepete eklemek niyetini belirtirse (örn. "sepete ekle",
        "varsa ekle", "ekler misin", "atar mısın") onay için tekrar SORMA; doğrudan
        add_to_cart aracını bulunan ürünün id'siyle çağır.
        Kullanıcı sadece arama yaptıysa (ekleme niyeti yoksa) sepete EKLEME, yalnızca sonucu döndür.
        Bir ürün bulunamazsa sepete ekleme yapma ve durumu kullanıcıya açıkça söyle.
        """;
}