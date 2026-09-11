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

    // 072: UCP katalog projeksiyonu TEK kuyruk dinler (Storefront deseni) — product.changed +
    // stock.changed aynı kuyruğa bağlanır, Sequential işlenir. Aynı UcpCatalogItem satırına eşzamanlı
    // yazım yapısal olarak imkânsızlaşır. Sipariş-olayı webhook'u AYRI kuyrukta (UcpOrderEvents).
    public static class UcpCatalogEvents
    {
        public const string Queue = "ucp.catalog-events";
    }

    // 072: UCP sipariş-olayı webhook tetiği — order.completed + order.canceled aynı kuyruğa bağlanır
    // (Sequential); UCP OrderEventsHandler tüketip imzalı webhook gönderir. Binding'i tüketici kurar.
    public static class UcpOrderEvents
    {
        public const string Queue = "ucp.order-events";
    }

    public static class ProductChanged
    {
        public const string Exchange = "product.changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;

            // 060: Library fiyat değişimini dinler (alarm tetiği); binding'i tüketici kurar (007).
            public const string Library = "library.events";

            // 072: UCP katalog projeksiyonu (tek sıralı kuyruk).
            public const string Ucp = UcpCatalogEvents.Queue;
        }
    }

    public static class StockChanged
    {
        public const string Exchange = "stock.changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;

            // 072: UCP katalog projeksiyonu (uygunluk = OnHand > 0).
            public const string Ucp = UcpCatalogEvents.Queue;
        }
    }

    // 072: Order yayınlar (bir siparişin iptali). UCP tüketir → `order.canceled` webhook'u.
    // OrderCompleted emsali; binding'i tüketici kurar.
    public static class OrderCanceled
    {
        public const string Exchange = "order.canceled";

        public static class Queues
        {
            public const string Ucp = UcpOrderEvents.Queue;
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

    // Order yayınlar (checkout başarı = Confirm pivotu). Reviews tüketir (satın-alma kanıtı projeksiyonu).
    // 054: Storefront da tüketir (kişisel feed UserPurchase birikimi) — mevcut tek kuyruğuna bağlanır.
    public static class OrderCompleted
    {
        public const string Exchange = "order.completed";

        public static class Queues
        {
            public const string Reviews = "reviews.order-completed";
            public const string Storefront = StorefrontEvents.Queue;

            // 072: UCP `order.confirmed` webhook tetiği (order.canceled ile aynı kuyruk, Sequential).
            public const string Ucp = UcpOrderEvents.Queue;
        }
    }

    // 060: Library yayınlar (üründeki her alarm için bir event), NotificationAgent tüketir
    // (worker kendi kuyruğunu bağlar).
    public static class PriceAlarmTriggered
    {
        public const string Exchange = "library.price-alarm-triggered";

        public static class Queues
        {
            public const string Worker = "notifications.price-alarm-triggered";
        }
    }

    // 060: NotificationAgent yayınlar (gönderim sonucu), Library tüketir → NotificationRecord izi.
    public static class NotificationSent
    {
        public const string Exchange = "notifications.sent";

        public static class Queues
        {
            public const string Library = "library.notifications-sent";
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