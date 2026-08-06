using System.Security.Claims;
using Identity.Server.Rbac;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// Açılışta idempotent client + scope seed (Duende in-memory store'un karşılığı).
// Varsa güncelle, yoksa yarat; secret'lar bugünkü düz değerler (store hash'ler).
public sealed class SeedHostedService(IServiceProvider provider) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = provider.CreateScope();
        var apps = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        // Scope'lar (audience/resource eşlemesiyle) — ListResourcesAsync bunlardan 'aud' üretir.
        foreach (var name in Config.AllApiScopes)
        {
            var descriptor = new OpenIddictScopeDescriptor { Name = name, DisplayName = name };
            if (Config.ScopeResources.TryGetValue(name, out var resource))
                descriptor.Resources.Add(resource);

            var existing = await scopes.FindByNameAsync(name, ct);
            if (existing is null)
                await scopes.CreateAsync(descriptor, ct);
            else
                await scopes.UpdateAsync(existing, descriptor, ct);
        }

        // İstemciler.
        foreach (var client in Config.Clients)
        {
            var descriptor = BuildDescriptor(client);
            var existing = await apps.FindByClientIdAsync(client.ClientId, ct);
            if (existing is null)
                await apps.CreateAsync(descriptor, ct);
            else
                await apps.UpdateAsync(existing, descriptor, ct);
        }

        // 030 RBAC: roller + rol→scope map + bootstrap admin + backfill (idempotent).
        await SeedRbacAsync(scope.ServiceProvider, ct);
    }

    // 030 RBAC seed. Roller ve rol→scope map yalnız YOKken doldurulur; admin'in ekrandan
    // yaptığı scope düzenlemeleri yeniden başlatmada EZİLMEZ.
    private static async Task SeedRbacAsync(IServiceProvider sp, CancellationToken ct)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();

        // Roller (admin + customer).
        foreach (var roleName in Config.RoleScopeSeed.Keys)
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));

        // Rol→scope map — yalnız o rol için hiç satır yoksa doldur.
        foreach (var (roleName, bundle) in Config.RoleScopeSeed)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;

            if (await db.RoleScopes.AnyAsync(rs => rs.RoleId == role.Id, ct)) continue;

            foreach (var s in bundle.Distinct())
                db.RoleScopes.Add(new RoleScope { Id = Guid.NewGuid(), RoleId = role.Id, Scope = s });
        }
        await db.SaveChangesAsync(ct);

        // Bootstrap admin — email+parola config'ten (kodda değil); yoksa oluşturma atlanır.
        var adminEmail = config["BootstrapAdmin:Email"];
        var adminPassword = config["BootstrapAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword)
            && await userManager.FindByNameAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var created = await userManager.CreateAsync(admin, adminPassword);
            if (created.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, RoleAssignmentService.AdminRole);
                await userManager.AddClaimsAsync(admin,
                    [new Claim(Claims.Name, adminEmail), new Claim(Claims.Email, adminEmail)]);
            }
        }

        // Backfill: rolsüz mevcut kullanıcılar → customer (feature öncesi kayıtlılar).
        foreach (var user in await userManager.Users.ToListAsync(ct))
            if ((await userManager.GetRolesAsync(user)).Count == 0)
                await userManager.AddToRoleAsync(user, RoleAssignmentService.CustomerRole);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static OpenIddictApplicationDescriptor BuildDescriptor(ClientSeed client)
    {
        var d = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            DisplayName = client.DisplayName,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
        };

        if (client.AllowAuthorizationCode)
        {
            d.Permissions.Add(Permissions.Endpoints.Authorization);
            d.Permissions.Add(Permissions.Endpoints.EndSession);
            d.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            d.Permissions.Add(Permissions.ResponseTypes.Code);
            d.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        if (client.AllowClientCredentials)
            d.Permissions.Add(Permissions.GrantTypes.ClientCredentials);

        if (client.AllowRefreshToken)
            d.Permissions.Add(Permissions.GrantTypes.RefreshToken);

        // Token ucu tüm grant'lar için gerekli.
        d.Permissions.Add(Permissions.Endpoints.Token);

        foreach (var uri in client.RedirectUris)
            d.RedirectUris.Add(new Uri(uri));
        foreach (var uri in client.PostLogoutRedirectUris)
            d.PostLogoutRedirectUris.Add(new Uri(uri));

        // Scope izinleri (scp: prefix'li). openid dahil — OpenIddict openid'i zaten serbest sayar.
        foreach (var s in client.Scopes)
            d.Permissions.Add(Permissions.Prefixes.Scope + s);

        return d;
    }
}