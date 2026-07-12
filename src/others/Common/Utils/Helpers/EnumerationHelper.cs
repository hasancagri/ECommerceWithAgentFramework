using Common.Domains;

namespace Common.Utils.Helpers;

public  static class EnumerationHelper
{
    public static List<T> Convert<T>(List<string> value) where T : Enumeration
    {
        var allItems = Enumeration.GetAll<T>();

        return value
            .Select(v =>
                allItems.FirstOrDefault(g => g.Name.Equals(v, StringComparison.OrdinalIgnoreCase)))
            .Where(g => g != null)
            .ToList();
    }
}
