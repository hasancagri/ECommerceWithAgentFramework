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

    // 050/051: Catalog yeni ürün YAYINLANINCA yayınlar, Stock barkod↔ProductId eşlemesini kurar + ilk OnHand.
    // Tüketici başına TEK sıralı kuyruk (aynı barkod sıralı işlenir); binding'i tüketici kurar (007).
    public static class ProductAdded
    {
        public const string Exchange = "catalog.product-added";

        public static class Queues
        {
            public const string Stock = "stock.product-added";
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

    // 048: Order yayınlar (checkout başarı), Personalization tüketir (kendi kuyruğunu bağlar, 007).
    // 049: Reviews da tüketir — satın-alma kanıtı read-model'i (gRPC yerine event-fed projeksiyon).
    public static class OrderCompleted
    {
        public const string Exchange = "order.completed";

        public static class Queues
        {
            public const string Personalization = "personalization.order-completed";
            public const string Reviews = "reviews.order-completed";
        }
    }

    // 049: Checkout orchestrator hedefli komut/yanıt (broker; İlke I v1.11.0). Her BC kendi komut
    // kuyruğunu bağlar; yanıtlar orchestrator'ın tek yanıt kuyruğuna döner (korelasyon = CheckoutId).
    public static class Checkout
    {
        // Giriş: WebApp endpoint + chat (Order) StartCheckout'u buraya yayınlar; orchestrator dinler → saga doğar.
        public const string StartQueue = "checkout.start";

        // Orchestrator → hedef BC komut kuyrukları (tüketici bağlar).
        public const string OrderCommandsQueue = "checkout.order-commands";
        public const string PaymentCommandsQueue = "checkout.payment-commands";
        public const string StockCommandsQueue = "checkout.stock-commands";
        public const string BasketCommandsQueue = "checkout.basket-commands";

        // Hedef BC → orchestrator yanıt kuyruğu (orchestrator bağlar).
        public const string RepliesQueue = "checkout.replies";
    }
}