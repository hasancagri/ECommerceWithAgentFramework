using Duende.IdentityServer.EntityFramework.DbContexts;
using Identity.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Aspire çalışma anında enjekte eder; design-time (migration üretimi) için fallback.
var connectionString = builder.Configuration.GetConnectionString("identityDb")
                       ?? "Host=localhost;Port=5432;Database=identityDb;Username=postgres;Password=postgres";

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// "Beni hatırla" işaretlenince persistent cookie bu süre kadar yaşar (varsayılan 14 gün yerine).
builder.Services.ConfigureApplicationCookie(options =>
    options.ExpireTimeSpan = Identity.Server.Pages.Login.LoginOptions.RememberMeLoginDuration);

builder.Services.AddIdentityServer(options =>
    {
        // WebApp "Sign Up" akisi authorize istegine prompt=create gonderir.
        // CreateAccountUrl set edilince Duende "create" prompt mode'unu destekler
        // (PromptValuesSupported'a eklenir) ve istegi dogrudan bu sayfaya yonlendirir;
        // aksi halde authorize/PAR dogrulamasi "Unsupported prompt mode" (400) ile reddeder.
        options.UserInteraction.CreateAccountUrl = "/Account/Create";
    })
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryClients(Config.Clients)
    .AddOperationalStore(options =>
        options.ConfigureDbContext = b =>
            b.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)))
    .AddAspNetIdentity<ApplicationUser>();

var app = builder.Build();

// Açılışta migration'ları uygula (dev kolaylığı; Postgres Aspire ile hazır olur).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
}

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();

app.MapRazorPages().RequireAuthorization();

app.Run();