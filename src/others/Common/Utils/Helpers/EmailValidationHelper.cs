using System.Text.RegularExpressions;

namespace Common.Utils.Helpers;
public static class EmailValidationHelper
{
    /// <summary>
    /// Validates whether the provided email address is in a valid format.
    /// </summary>
    /// <param name="email">The email address to validate.</param>
    /// <returns>Returns true if the email address is valid; otherwise, false.</returns>
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        // Regular expression for validating email format
        var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email.Trim(), emailRegex);
    }
}