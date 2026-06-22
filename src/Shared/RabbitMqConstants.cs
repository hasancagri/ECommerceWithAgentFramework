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
}