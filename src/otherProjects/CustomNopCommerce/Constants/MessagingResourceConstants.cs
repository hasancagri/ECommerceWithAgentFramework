namespace CustomNopCommerce.Constants;

/// <summary>Messaging bounded context'inin hata/mesaj kodları (şablon + newsletter + e-posta kuyruğu).</summary>
public static class MessagingResourceConstants
{
    public const string RECORD_NOT_FOUND = "MESSAGING_RECORD_NOT_FOUND";

    public const string TEMPLATE_NAME_REQUIRED = "MESSAGING_TEMPLATE_NAME_REQUIRED";
    public const string TEMPLATE_SUBJECT_REQUIRED = "MESSAGING_TEMPLATE_SUBJECT_REQUIRED";

    public const string SUBSCRIPTION_EMAIL_REQUIRED = "MESSAGING_SUBSCRIPTION_EMAIL_REQUIRED";
    public const string SUBSCRIPTION_EMAIL_INVALID = "MESSAGING_SUBSCRIPTION_EMAIL_INVALID";
    public const string SUBSCRIPTION_ALREADY_EXISTS = "MESSAGING_SUBSCRIPTION_ALREADY_EXISTS";
    public const string SUBSCRIPTION_ALREADY_INACTIVE = "MESSAGING_SUBSCRIPTION_ALREADY_INACTIVE";

    public const string EMAIL_RECIPIENT_REQUIRED = "MESSAGING_EMAIL_RECIPIENT_REQUIRED";
    public const string EMAIL_ALREADY_SENT = "MESSAGING_EMAIL_ALREADY_SENT";
}
