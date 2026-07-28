namespace Catalog.Api.Domains.Brands;

// 016: yalnız feed'den get-or-create ile doğar (R2); ad immutable, rename yok.
// NormalizedName teklik anahtarıdır (computed unique index, Program.cs).
public class Brand : AggregateRoot
{
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }

    private Brand()
    {
    }

    // JasperFxIgnore: tek parametreli statik Create, event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<Brand> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Brand>.Error(new MessageItem
            {
                Property = nameof(Name),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_EMPTY
            });

        var normalized = NameNormalization.Normalize(name);
        return ResultDomain<Brand>.Ok(new Brand
        {
            Name = name.Trim(),
            NormalizedName = normalized
        });
    }
}