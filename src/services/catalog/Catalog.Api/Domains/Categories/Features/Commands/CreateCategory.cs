
namespace Catalog.Api.Domains.Categories.Features.Commands;

public static class CreateCategory
{
    public record CreateCategoryCommand(string Name);
    
    [Transactional]
    public class CreateCategoryHandler(IDocumentSession session)
    {
        public Task Handle(CreateCategoryCommand cmd, CancellationToken ct)
        {
            var category = Category.Create(cmd.Name);
            session.Store(category);
            return Task.CompletedTask;
        }
    }
}

public static class CreateCategoryEndpoint
{
    public static RouteGroupBuilder CreateCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateCategory.CreateCategoryCommand cmd, IMessageBus bus) =>
            {
                await bus.InvokeAsync(cmd);
                return Results.Ok();
            })
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);
        return group;
    }
}