namespace Common.Utils.Helpers;
public static class UniqueValueGeneratorHelper
{
    private static readonly HashSet<long> UniqueValues = new HashSet<long>();
    private static readonly HashSet<long> UniquePasswords = new HashSet<long>();

    public static string GenerateUniqueValue()
    {
        string newValue;
        long hashCode;
        do
        {
            newValue = GenerateRandomString(11);
            hashCode = newValue.GetHashCode();
        } while (UniqueValues.Contains(hashCode));

        UniqueValues.Add(hashCode);
        return newValue;
    }

    public static string GenerateUniquePassword()
    {
        string newValue;
        long hashCode;
        do
        {
            newValue = GenerateRandomString(8);
            hashCode = newValue.GetHashCode();
        } while (UniquePasswords.Contains(hashCode));

        UniqueValues.Add(hashCode);
        return newValue;
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        Random random = new Random();
        return new string(Enumerable.Repeat(chars, length)
          .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}