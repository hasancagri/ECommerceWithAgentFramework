namespace Common.Utils.Caching;

/// <summary>
/// Caching aspect'inin servise-özel ayarları. KeyPrefix, anahtarları bounded-context düzeyinde
/// ayırır (ör. "catalog"); aynı Redis örneğinde farklı servislerin anahtarları çakışmaz.
/// </summary>
public sealed class CacheAspectOptions
{
    public required string KeyPrefix { get; init; }
}