namespace Common.Utils.Helpers;

public static class QueryStringHelper
{
    public static List<string> SplitValue(string value)
    {
        var items = new List<string>();
        if (string.IsNullOrEmpty(value))
        {
            return items;
        }

        var splitValues = value.Split("|");
        items.AddRange(new List<string>(splitValues));
        var result = new List<string>();
        foreach (var item in items)
        {
            var isValidGuid = Guid.TryParse(item, out var guidOutput);
            if (!isValidGuid && item.Contains('-'))
            {
                var lastItem = item.Split("-").LastOrDefault();
                result.Add(lastItem!);
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }
}
