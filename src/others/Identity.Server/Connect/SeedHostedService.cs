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