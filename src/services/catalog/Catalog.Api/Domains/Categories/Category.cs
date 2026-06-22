
namespace Catalog.Api.Domains.Categories;

public class Category : AggregateRoot
{
    public string Name { get; private set; } 

    private Category() { }

    public static Category Create(string name)
    {
        return new Category { Name = name };
    }
}