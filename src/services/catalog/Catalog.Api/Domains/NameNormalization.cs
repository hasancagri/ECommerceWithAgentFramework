namespace Catalog.Api.Domains;

// 016: Category/Brand teklik anahtarı üretimi (research R3). Fabrikalar ve get-or-create
// sorguları AYNI fonksiyonu kullanır ki eşleşme iki tarafta da tutarlı olsun.
public static class NameNormalization
{
    public static string Normalize(string name)
    {
        var collapsed = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");
        return collapsed.ToUpperInvariant();
    }
}