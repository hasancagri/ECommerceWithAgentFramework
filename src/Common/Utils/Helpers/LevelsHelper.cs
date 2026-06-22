namespace Common.Utils.Helpers;

public static class LevelsHelper
{
    public static string GetLevel(Dictionary<string, (int Min, int Max)> levels, int value)
    {
        foreach (var range in levels.Where(range => value >= range.Value.Min && value <= range.Value.Max))
        {
            return range.Key;
        }

        return string.Empty;
    }
}
