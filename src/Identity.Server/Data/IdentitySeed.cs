using System.Security.Claims;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace Identity.Server;

// Acilista rolleri ve seed admin kullanicisini olusturur. Idempotent: her acilista
// guvenle calisir, var olanlara dokunmaz. Hata uygulamayi bloklamaz; ILogger'a yazilir.
public static class IdentitySeed
{
    public static async Task SeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        // 1) Roller (Admin, Customer) yoksa olustur.
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Customer })
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
                logger.LogWarning("Rol '{Role}' olusturulamadi: {Errors}",
                    role, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // 2) Admin kullanici (config'den). Bilgi yoksa atla.
        var email = app.Configuration["SeedAdmin:Email"];
        var password = app.Configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedAdmin:Email/Password bos; admin kullanici seed'i atlandi.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(email) is not null)
            return; // zaten var, idempotent

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogWarning("Admin kullanici olusturulamadi: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, Roles.Admin);
        await userManager.AddClaimsAsync(user,
        [
            new Claim(JwtClaimTypes.Name, email),
            new Claim(JwtClaimTypes.Email, email),
        ]);
        logger.LogInformation("Seed admin kullanicisi olusturuldu: {Email}", email);
    }
}