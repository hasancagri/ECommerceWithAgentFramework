using System.Security.Claims;

namespace Common.Auths;

public class CurrentUser : ICurrentUser
{
    public static ICurrentUser Load(ClaimsPrincipal principal) => new CurrentUser
    {
        Id = Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty,
        Email = principal.FindFirst("email")?.Value,
        Name = string.Join(" ", new[]
        {
            principal.FindFirst("given_name")?.Value,
            principal.FindFirst("family_name")?.Value,
        }.Where(s => s is not null)),
    };

    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}