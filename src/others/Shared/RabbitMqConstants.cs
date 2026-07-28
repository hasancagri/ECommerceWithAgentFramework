namespace Shared;

public static class RabbitMqConstants
{
    public static class OrderCreated
    {
        public const string Exchange = "order.created";

        public static class Queues
        {
            public const string Basket = "basket.order-created";
        }
    }

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

    // 007: Supplier.Gateway yayınlar, IngestionAgent tüketir; retry tükenince DLQ'ya düşer.
    public static class SupplierProductSnapshot
    {
        public const string Exchange = "supplier.product-snapshot";
        public const string DeadLetter = "ingestion.supplier-product-snapshot.dlq";

        public static class Queues
        {
            public const string Ingestion = "ingestion.supplier-product-snapshot";
        }
    }
}