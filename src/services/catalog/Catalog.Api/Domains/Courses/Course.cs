
namespace Catalog.Api.Domains.Courses;

public class Course : AggregateRoot
{
    public string Name { get; private set; } 
    public string Description { get; private set; } 
    public decimal Price { get; private set; }
    public Guid UserId { get; private set; }
    public string? ImageUrl { get; private set; }
    public Guid CategoryId { get; private set; }
    public Feature Feature { get; private set; } 

    private Course()
    {
    }

    public static Course Create(string name, string description, decimal price, Guid userId, Guid categoryId,
        string? imageUrl, Feature feature)
    {
        return new Course
        {
            Name = name,
            Description = description,
            Price = price,
            UserId = userId,
            CategoryId = categoryId,
            ImageUrl = imageUrl,
            Feature = feature
        };
    }

    public void Update(string name, string description, decimal price, string? imageUrl, Guid categoryId, Feature feature)
    {
        Name = name;
        Description = description;
        Price = price;
        ImageUrl = imageUrl;
        CategoryId = categoryId;
        Feature = feature;
    }

    public void UpdateImageUrl(string imageUrl)
    {
        ImageUrl = imageUrl;
    }

    public void Delete()
    {
        IsDeleted = true;
    }
}