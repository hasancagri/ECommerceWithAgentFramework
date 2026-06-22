using System.Globalization;
using System.Text;

namespace Common.Utils.Extensions;

public static class StringExtensions
{
  
    public static string ToSafeText(this string incomingText)
    {
        if (string.IsNullOrEmpty(incomingText))
            incomingText = string.Empty;

        incomingText = string.Join("", incomingText.Normalize(NormalizationForm.FormD)
            .Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));

        incomingText = incomingText.Trim();
        incomingText = incomingText.ToLower();
        incomingText = incomingText.Replace("ş", "s");
        incomingText = incomingText.Replace("Ş", "s");
        incomingText = incomingText.Replace("İ", "i");
        incomingText = incomingText.Replace("I", "i");
        incomingText = incomingText.Replace("ı", "i");
        incomingText = incomingText.Replace("ö", "o");
        incomingText = incomingText.Replace("Ö", "o");
        incomingText = incomingText.Replace("ü", "u");
        incomingText = incomingText.Replace("Ü", "u");
        incomingText = incomingText.Replace("Ç", "c");
        incomingText = incomingText.Replace("ç", "c");
        incomingText = incomingText.Replace("ğ", "g");
        incomingText = incomingText.Replace("Ğ", "g");
        incomingText = incomingText.Replace(" ", "_");
        incomingText = incomingText.Replace("  ", "_");

        incomingText = incomingText.Replace("?", "");
        incomingText = incomingText.Replace("/", "");
        incomingText = incomingText.Replace(".", "");
        incomingText = incomingText.Replace("'", "");
        incomingText = incomingText.Replace("#", "");
        incomingText = incomingText.Replace("%", "");
        incomingText = incomingText.Replace("&", "");
        incomingText = incomingText.Replace("*", "");
        incomingText = incomingText.Replace("!", "");
        incomingText = incomingText.Replace(",", "_");
        incomingText = incomingText.Replace("@", "");
        incomingText = incomingText.Replace("+", "");
        incomingText = incomingText.Replace("<b>", "");
        incomingText = incomingText.Replace("</b>", "");
        incomingText = incomingText.Replace(";", "");
        incomingText = incomingText.Replace(":", "");
        incomingText = incomingText.Replace("<br>", "");
        incomingText = incomingText.Replace("<br/>", "");
        incomingText = incomingText.Replace('"'.ToString(), "");
        incomingText = incomingText.Replace("®", "");
        incomingText = incomingText.Replace("’", "");
        incomingText = incomingText.Replace("-", "_");
        incomingText = incomingText.Replace("___", "_");
        incomingText = incomingText.Replace("__", "_");
        incomingText = incomingText.Replace("\u00a0", "_");

        incomingText = incomingText.Trim();
        return incomingText;
    }

    public static int ToInt(this string input)
    {
        int intValue = int.TryParse(input, out intValue) ? intValue : 0;
        return intValue;
    }
}
