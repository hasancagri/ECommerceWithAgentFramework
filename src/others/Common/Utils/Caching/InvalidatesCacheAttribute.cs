namespace Common.Utils.Caching;

/// <summary>
/// Bir yazma komutu (command record) bu işaretle, başarılı çalışması sonrası ilgili etikete ait
/// tüm önbellek girdilerini iki katmandan boşaltır. Boşaltma commit sonrası yapılır (bkz.
/// <see cref="CachingMessageBus"/>): Wolverine komut handler'ı [Transactional] olduğundan
/// InvokeAsync döndüğünde commit tamamlanmıştır.
/// </summary>
/// <param name="tag">Boşaltılacak etiket (v1 kaba taneli: "catalog-products").</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InvalidatesCacheAttribute(string tag) : Attribute
{
    public string Tag { get; } = tag;
}