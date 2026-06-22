namespace Common.Utils.Helpers;

public static class PhoneValidationHelper
{
    /// <summary>
    /// Validates and formats a phone number based on specific rules.
    /// </summary>
    /// <param name="phone">The phone number to validate and format.</param>
    /// <param name="langId">The language ID for specific validation rules.</param>
    /// <returns>Returns the formatted phone number if valid; otherwise, throws an exception.</returns>
    public static string? PhoneValidFormat(string phone, int langId)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;


        var cleanedPhone = new string(phone.Where(char.IsDigit).ToArray());


        // Ensure the phone number contains only digits
        if (!cleanedPhone.All(char.IsDigit))
        {
            return null;
        }

        // 10 haneli ise (örn: 5331234567 veya 2126156895)
        if (cleanedPhone.Length == 10)
        {
            // Mobil numara (5XX) ise başına '0' ekle
            if (cleanedPhone.StartsWith("5"))
                cleanedPhone = "0" + cleanedPhone;
            // Sabit hat (2XX veya 3XX) ise başına '0' ekle
            else if (cleanedPhone.StartsWith("2") || cleanedPhone.StartsWith("3") || cleanedPhone.StartsWith("4"))
                cleanedPhone = "0" + cleanedPhone;
            else
                return null; // Geçersiz başlangıç
        }

        if (cleanedPhone.Length == 12 && cleanedPhone.StartsWith("90"))
        {
            // '90' kaldırılıp '0' eklenir (905331234567 -> 05331234567)
            cleanedPhone = "0" + cleanedPhone.Substring(2);
        }

        // If phone number is not 11 digits, it's invalid
        if (cleanedPhone.Length != 11)
        {
            return null;
        }

        return cleanedPhone;
    }
}
