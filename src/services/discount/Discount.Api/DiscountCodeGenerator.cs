namespace Discount.Api;

public static class DiscountCodeGenerator
{
    private const string Allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static string Generate(int length = 10)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        var buffer = new char[length];
        for (var i = 0; i < length; i++)
        {
            var idx = RandomNumberGenerator.GetInt32(Allowed.Length); // uniform, bias yok
            buffer[i] = Allowed[idx];
        }

        return new string(buffer);
    }
}