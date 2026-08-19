namespace CustomNopCommerce.Constants;

/// <summary>Shipping bounded context'inin hata/mesaj kodları (yöntem + depo + sevkiyat).</summary>
public static class ShippingResourceConstants
{
    public const string RECORD_NOT_FOUND = "SHIPPING_RECORD_NOT_FOUND";

    public const string METHOD_NAME_REQUIRED = "SHIPPING_METHOD_NAME_REQUIRED";
    public const string METHOD_RATE_INVALID = "SHIPPING_METHOD_RATE_INVALID";

    public const string WAREHOUSE_NAME_REQUIRED = "SHIPPING_WAREHOUSE_NAME_REQUIRED";

    public const string SHIPMENT_NO_ITEMS = "SHIPPING_SHIPMENT_NO_ITEMS";
    public const string SHIPMENT_TRACKING_REQUIRED = "SHIPPING_SHIPMENT_TRACKING_REQUIRED";
    public const string SHIPMENT_ALREADY_SHIPPED = "SHIPPING_SHIPMENT_ALREADY_SHIPPED";
    public const string SHIPMENT_NOT_SHIPPED_YET = "SHIPPING_SHIPMENT_NOT_SHIPPED_YET";
    public const string SHIPMENT_ITEM_QUANTITY_INVALID = "SHIPPING_SHIPMENT_ITEM_QUANTITY_INVALID";
}
