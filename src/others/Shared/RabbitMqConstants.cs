namespace Shared;

public static class RabbitMqConstants
{
    // 028: OrderCreated exchange kaldirildi — sepet temizligi artik CheckoutSaga'nin gRPC adimidir.

    public static class UploadCoursePicture
    {
        public const string Exchange = "upload.course-picture";

        public static class Queues
        {
            public const string File = "file.upload-course-picture";
        }
    }

    public static class CoursePictureUploaded
    {
        public const string Exchange = "course.picture.uploaded";

        public static class Queues
        {
            public const string Catalog = "catalog.course-picture-uploaded";
        }
    }

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

    // 041: Procurement yayınlar, Catalog tüketir (eksiksiz kanonik ürün, fat).
    public static class CanonicalProduct
    {
        public const string Exchange = "procurement.canonical-product";

        public static class Queues
        {
            public const string Catalog = ProcurementEvents.CatalogQueue;
        }
    }

    // 041: Procurement yayınlar, Catalog (fiyat) + Stock (OnHand) tüketir.
    public static class BuyBoxChanged
    {
        public const string Exchange = "procurement.buybox-changed";

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
}