namespace Common.Utils.Extensions;

public static class ObjectExtensions
{
    public static int ToInt(this object value)
    {
        return Convert.ToInt32(value);
    }
}
