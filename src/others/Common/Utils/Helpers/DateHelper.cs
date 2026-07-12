using System.Globalization;

namespace Common.Utils.Helpers;

public static class DateHelper
{
    // Her zaman UTC döner; eski culture parametresi yok sayılıyordu, kaldırıldı.
    public static DateTime Now()
    {
        return DateTime.UtcNow;
    }

    public static string ToDateString(this DateTime dateTime, string format = "dd.MM.yyyy")
    {
        return dateTime.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string ToDateStringWithCulture(this DateTime dateTime, string format = "dd.MM.yyyy", string langCode = "tr")
    {
        return langCode switch
        {
            "tr" => dateTime.ToString(format, new CultureInfo("tr-TR")),
            "en" => dateTime.ToString(format, new CultureInfo("en-US")),
            "pt" => dateTime.ToString(format, new CultureInfo("pt-PT")),
            "de" => dateTime.ToString(format, new CultureInfo("de-DE")),
            "nl" => dateTime.ToString(format, new CultureInfo("nl-NL")),
            "id" => dateTime.ToString(format, new CultureInfo("id-ID")),
            "fr" => dateTime.ToString(format, new CultureInfo("fr-FR")),
            "es" => dateTime.ToString(format, new CultureInfo("es-ES")),
            _ => dateTime.ToString(format)
        };
    }

    public static int DifferenceOfMonth(DateTime start, DateTime end)
    {
        var totalDays = (start - end).TotalDays;
        return (int)Math.Round(totalDays / (365.2425 / 12), MidpointRounding.ToZero);
    }

    public static int DifferenceOfYear(DateTime start, DateTime end)
    {
        var totalDays = (start - end).TotalDays;
        return (int)Math.Round(totalDays / 365.2425, MidpointRounding.ToZero);
    }

    public static DateTime? TryParse(string? dateString, string culture = "tr-TR")
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return null;
        }
        var cultureInfo = new CultureInfo(culture!);
        var tryParseResult = DateTime.TryParse(dateString, cultureInfo, DateTimeStyles.None, out var date);
        if (!tryParseResult)
        {
            return null;
        }

        return date;
    }

    public static DateTime? TryParse(string dateString, List<string> cultures)
    {
        foreach (var cultureInfo in cultures.Select(culture => new CultureInfo(culture!)))
        {
            var tryParseResult = DateTime.TryParse(dateString, cultureInfo, DateTimeStyles.None, out var date);
            if (!tryParseResult)
            {
                continue;
            }

            return date;
        }

        return null;
    }
}
