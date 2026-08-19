namespace CustomNopCommerce.Constants;

/// <summary>Directory bounded context'inin hata/mesaj kodları (ülke/il + para birimi + ölçü birimi).</summary>
public static class DirectoryResourceConstants
{
    public const string RECORD_NOT_FOUND = "DIRECTORY_RECORD_NOT_FOUND";

    public const string COUNTRY_NAME_REQUIRED = "DIRECTORY_COUNTRY_NAME_REQUIRED";
    public const string STATE_NAME_REQUIRED = "DIRECTORY_STATE_NAME_REQUIRED";

    public const string CURRENCY_NAME_REQUIRED = "DIRECTORY_CURRENCY_NAME_REQUIRED";
    public const string CURRENCY_CODE_REQUIRED = "DIRECTORY_CURRENCY_CODE_REQUIRED";
    public const string CURRENCY_RATE_INVALID = "DIRECTORY_CURRENCY_RATE_INVALID";

    public const string MEASURE_NAME_REQUIRED = "DIRECTORY_MEASURE_NAME_REQUIRED";
    public const string MEASURE_RATIO_INVALID = "DIRECTORY_MEASURE_RATIO_INVALID";
}
