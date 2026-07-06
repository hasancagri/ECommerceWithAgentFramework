using System.Reflection;

namespace Common.Domains;

public abstract class Enumeration : IComparable
{
    public string Name { get; private set; }

    public int Id { get; private set; }

    protected Enumeration(int id, string name) => (Id, Name) = (id, name);

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<T>();

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration otherValue)
        {
            return false;
        }

        var typeMatches = GetType().Equals(obj.GetType());
        var valueMatches = Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static int AbsoluteDifference(Enumeration firstValue, Enumeration secondValue)
    {
        var absoluteDifference = Math.Abs(firstValue.Id - secondValue.Id);
        return absoluteDifference;
    }

    // Eşleşme yoksa null döner; eski try/catch akış kontrolü kaldırıldı.
    public static T? FromValue<T>(int value) where T : Enumeration =>
        GetAll<T>().FirstOrDefault(item => item.Id == value);

    public static T? FromDisplayName<T>(string displayName) where T : Enumeration =>
        GetAll<T>().FirstOrDefault(item => item.Name == displayName);

    public int CompareTo(object? obj) => Id.CompareTo(((Enumeration)obj!).Id);
}