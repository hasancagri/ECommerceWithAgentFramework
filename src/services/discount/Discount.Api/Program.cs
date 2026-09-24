using Discount.Api.Domains.ProductCatalogRefs;
using Discount.Api.Domains.ProductDiscounts;
using Discount.Api.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddDiscountMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddDiscountMessaging();

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.DiscountRead,
    AuthorizationScopes.AdminDiscountWrite);

// 070: /mcp-admin RFC 9728 keşfi (401 challenge + metadata) — admin scope'uyla. Anonim /mcp YOK.
builder.Services.AddMcpAdminResourceMetadata(builder.Configuration, "discount",
    AuthorizationScopes.AdminDiscountWrite);

builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();

// 070/074: tek MCP server, YALNIZ korumalı /mcp-admin ucu (kampanya = admin işi). Oturum başına taze
// options; tool seti yol-prefix'iyle budanır — allowlist YALNIZ /mcp-admin'de görünür (yeni tool → allowlist'e EKLE).
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, mcpOptions, _) =>
    {
        var isAdmin = ctx.Request.Path.StartsWithSegments("/mcp-admin");
        var tools = mcpOptions.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => Discount.Api.Mcp.DiscountAdminSurface.ToolNames.Contains(t.ProtocolTool.Name) != isAdmin).ToArray())
            tools.Remove(tool);
        return Task.CompletedTask;
    })
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 079 US3: checkout S2S — Order.Api canlı indirim doğrulaması (discount.read).
app.MapGrpcService<DiscountQueryGrpcService>().RequireAuthorization(AuthorizationScopes.DiscountRead);

// 070: korumalı yönetim ucu — kimliksiz istek 401 + resource_metadata challenge.
app.MapMcp("/mcp-admin").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();
