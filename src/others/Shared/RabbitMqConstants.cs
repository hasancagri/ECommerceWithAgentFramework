namespace Shared;

public static class RabbitMqConstants
{
    // 028: OrderCreated exchange kaldirildi — sepet temizligi artik CheckoutSaga'nin gRPC adimidir.

    // Storefront TEK kuyruk dinler: üç exchange de aynı kuyruğa bağlanır ve Sequential işlenir.
    // Aynı StorefrontView satırına eşzamanlı yazım yapısal olarak imkânsızlaşır (ConcurrencyException
    // kaynağı yok edilir; Program.cs'teki retry kuralı yedek güvence olarak durur).
    public static class StorefrontEvents
    {
        public const string Queue = "storefront.events";
    }

    public static class ProductChanged
    {
        public const string Exchange = "product.changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;
        }
    }

    public static class StockChanged
    {
        public const string Exchange = "stock.changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;
        }
    }

    // 012: TTL dolunca Stock yayınlar, Basket tüketip sepet satırını siler (fanout).
    public static class ReservationExpired
    {
        public const string Exchange = "stock.reservation-expired";

        public static class Queues
        {
            public const string Basket = "basket.reservation-expired";
        }
    }

    // 041: tüketici başına TEK sıralı kuyruk (storefront.events emsali) — aynı barkodun
    // event'leri sıralı işlenir. Catalog iki exchange'i, Stock iki exchange'i aynı kuyruğa bağlar.
    public static class ProcurementEvents
    {
        public const string CatalogQueue = "catalog.procurement-events";
        public const string StockQueue = "stock.procurement-events";
    }

    // 041/047: Procurement yayınlar; Catalog (içerik+fiyat) + Stock (OnHand) tüketir. 047'de buy-box
    // söküldü → fiyat/stok bu TEK kanaldan akar (ayrı BuyBoxChanged yok).
    public static class CanonicalProduct
    {
        public const string Exchange = "procurement.canonical-product";

        public static class Queues
        {
            public const string Catalog = ProcurementEvents.CatalogQueue;
            public const string Stock = ProcurementEvents.StockQueue;
        }
    }

    // 041: Catalog yeni üründe yayınlar, Stock barkod↔ProductId eşlemesini kurar.
    public static class ProductLinked
    {
        public const string Exchange = "catalog.product-linked";

        public static class Queues
        {
            public const string Stock = ProcurementEvents.StockQueue;
        }
    }

    // 044: Reviews yayınlar, Storefront satırına RatingAverage/RatingCount yazar.
    // Storefront TEK kuyruk deseni: mevcut storefront.events kuyruğuna bağlanır (Sequential).
    public static class ReviewSummaryChanged
    {
        public const string Exchange = "reviews.summary-changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;
        }
    }

    // 046: Reviews yayınlar, Reviews.Moderation worker tüketir (worker kendi kuyruğunu bağlar).
    public static class ReviewModerationRequested
    {
        public const string Exchange = "reviews.moderation-requested";

        public static class Queues
        {
            public const string Worker = "reviews-moderation.requested";
        }
    }

    // 046: Reviews.Moderation worker yayınlar, Reviews tüketir (Reviews kendi kuyruğunu bağlar).
    public static class ReviewModerated
    {
        public const string Exchange = "reviews.moderated";

        public static class Queues
        {
            public const string Reviews = "reviews.moderated";
        }
    }

    // 048: Order yayınlar (CheckoutSaga başarı), Personalization tüketir (kendi kuyruğunu bağlar, 007).
    public static class OrderCompleted
    {
        public const string Exchange = "order.completed";

        public static class Queues
        {
            public const string Personalization = "personalization.order-completed";
        }
    }
}