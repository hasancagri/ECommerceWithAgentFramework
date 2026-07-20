namespace Shared;

public static class RabbitMqConstants
{
    public static class OrderCreated
    {
        public const string Exchange = "order.created";

        public static class Queues
        {
            public const string Basket = "basket.order-created";
            public const string Discount = "discount.order-created";
        }
    }

    public static class ProductCreated
    {
        public const string Exchange = "product.created";

        public static class Queues
        {
            public const string Stock = "stock.product-created";
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

    public static class ProductChanged
    {
        public const string Exchange = "product.changed";

        public static class Queues
        {
            public const string Storefront = "storefront.product-changed";
        }
    }

    public static class StockChanged
    {
        public const string Exchange = "stock.changed";

        public static class Queues
        {
            public const string Storefront = "storefront.stock-changed";
        }
    }

    public static class DiscountChanged
    {
        public const string Exchange = "discount.changed";

        public static class Queues
        {
            public const string Storefront = "storefront.discount-changed";
        }
    }
}