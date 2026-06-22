
namespace Catalog.Api.Infrastructure;

public class SeedData : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken cancellation)
    {
        await using var session = store.LightweightSession();

        var categoryExists = await session.Query<Category>().AnyAsync(token: cancellation);
        if (categoryExists) return;

        var categories = new List<Category>
        {
            Category.Create("Development"),
            Category.Create("Business"),
            Category.Create("IT & Software"),
            Category.Create("Office Productivity"),
            Category.Create("Personal Development")
        };

        session.Store(categories.ToArray());
        await session.SaveChangesAsync(cancellation);

        var category = categories[0];
        var randomUserId = Guid.NewGuid();

        var courses = new List<Course>
        {
            Course.Create("C#", "C# Course", 100, randomUserId, category.Id, null,
                new Feature(10, 4, "Ahmet Yıldız")),
            Course.Create("Java", "Java Course", 200, randomUserId, category.Id, null,
                new Feature(10, 4, "Ahmet Yıldız")),
            Course.Create("Python", "Python Course", 300, randomUserId, category.Id, null,
                new Feature(10, 4, "Ahmet Yıldız"))
        };

        session.Store(courses.ToArray());
        await session.SaveChangesAsync(cancellation);
    }
}