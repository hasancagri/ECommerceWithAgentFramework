namespace Catalog.Api.Domains.Categories;

// 016: yalnız feed'den get-or-create ile doğar (R2); ad immutable, rename yok.
// NormalizedName teklik anahtarıdır (computed unique index, Program.cs).
public class Category : AggregateRoot
{
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }

    private Category()
    {
    }

    // JasperFxIgnore: tek parametreli statik Create, event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<Category> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Category>.Error(new MessageItem
            {
                Property = nameof(Name),
                Code = CommonResourceConstants.COMMON_MESSAGE_VALUE_EMPTY
            });

        var normalized = NameNormalization.Normalize(name);
        return ResultDomain<Category>.Ok(new Category
        {
            Name = name.Trim(),
            NormalizedName = normalized
        });
    }
}