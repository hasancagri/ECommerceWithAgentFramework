namespace Common.Utils.Helpers;
public static class DictionaryExtensions
{
    public static Guid? GetValueOrNull(this Dictionary<string, Guid>? dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return null;
        }
        return dictionary[key];
    }
}
