namespace CustomNopCommerce.Domains.GdprLogEntries;

/// <summary>
/// GDPR denetim kaydının türü. Rıza kabul/ret, veri dışa aktarım talebi, hesap silme talebi, profil değişimi.
/// nopCommerce GdprRequestType paritesi.
/// </summary>
public enum GdprRequestType
{
    ConsentAgree = 1,
    ConsentDisagree = 5,
    ExportData = 10,
    DeleteCustomer = 15,
    ProfileChanged = 20,
}
