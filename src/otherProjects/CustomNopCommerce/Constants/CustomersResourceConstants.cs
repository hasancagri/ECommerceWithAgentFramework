namespace CustomNopCommerce.Constants;

/// <summary>
/// Customers bounded context'inin hata/mesaj kodları. Customer AYRI BC'dir; kimlik/auth/parola/rol
/// buraya GİRMEZ (Identity.Server'ın işi) — bu BC yalnız profil + adres defteri tutar.
/// </summary>
public static class CustomersResourceConstants
{
    public const string RECORD_NOT_FOUND = "CUSTOMERS_RECORD_NOT_FOUND";

    public const string CUSTOMER_EMAIL_REQUIRED = "CUSTOMERS_EMAIL_REQUIRED";
    public const string CUSTOMER_EMAIL_INVALID = "CUSTOMERS_EMAIL_INVALID";
    public const string CUSTOMER_ALREADY_INACTIVE = "CUSTOMERS_ALREADY_INACTIVE";

    public const string ADDRESS_LINE_REQUIRED = "CUSTOMERS_ADDRESS_LINE_REQUIRED";
    public const string ADDRESS_CITY_REQUIRED = "CUSTOMERS_ADDRESS_CITY_REQUIRED";
    public const string ADDRESS_NOT_FOUND = "CUSTOMERS_ADDRESS_NOT_FOUND";
}
