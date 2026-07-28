namespace Storefront.Api.Domains.StorefrontView;

// 019: Rich aggregate DEGIL — urun basina bir anlamsal yan-kayit (read-model). StorefrontView'dan
// ayri dokuman: 1536 float, liste sorgularinda tasinmaz (R2). Vektor JSONB'de float[] durur;
// hibrit sorgu onu ::vector cast'iyle kullanir (R3). Satir silinmez; satilabilirlik filtresi eler.
public class ProductEmbedding
{
    public const int Dimensions = 1536; // text-embedding-3-small (R5)

    private ProductEmbedding()
    {
    }

    public Guid ProductId { get; private set; }

    // Arama metninin SHA-256'si — degisim tespiti: hash ayniysa embedding yeniden uretilmez (FR-013).
    public string TextHash { get; private set; } = null!;

    public float[] Embedding { get; private set; } = [];

    public DateTimeOffset UpdatedTime { get; private set; }

    public static ProductEmbedding Create(Guid productId, string textHash, float[] embedding) =>
        new()
        {
            ProductId = productId,
            TextHash = textHash,
            Embedding = embedding,
            UpdatedTime = DateTimeOffset.UtcNow
        };

    public void Refresh(string textHash, float[] embedding)
    {
        TextHash = textHash;
        Embedding = embedding;
        UpdatedTime = DateTimeOffset.UtcNow;
    }

    // Arama metni: Name + Description + Brand + Category; null/bos alanlar atlanir (data-model).
    public static string? BuildSearchText(string? name, string? description, string? brand, string? category)
    {
        var parts = new[] { name, description, brand, category }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        return parts.Length == 0 ? null : string.Join("\n", parts);
    }

    public static string ComputeTextHash(string searchText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(searchText)));
}