using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Server;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.KeyHash).IsRequired();
            e.Property(x => x.UserId).IsRequired();
            e.HasIndex(x => x.KeyHash).IsUnique();
        });

        builder.Entity<UserScope>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).IsRequired();
            e.Property(x => x.Scope).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Scope }).IsUnique();
        });

        // OpenIddict 4 tablosu (Applications/Authorizations/Scopes/Tokens) aynı context'e.
        builder.UseOpenIddict();
    }
}