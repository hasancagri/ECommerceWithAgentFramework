namespace Common.Utils.Extensions;

public static class DecimalExtensions
{
    public static int ToRound(this decimal value, int? decimals = 0)
    {
        var roundedValue = decimal.Round(value, decimals!.Value);
        return Convert.ToInt32(roundedValue);
    }
}