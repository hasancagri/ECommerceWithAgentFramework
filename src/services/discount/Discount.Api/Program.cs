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

// 085 R5: RFC 9728 keşfi (401 challenge + metadata) — /mcp slug'ında (admin scope'uyla). Anonim set YOK.
builder.Services.AddMcpResourceMetadata(builder.Configuration, "discount",
    AuthorizationScopes.AdminDiscountWrite);

builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();

// 085 R5: tek MCP server, YALNIZ korumalı /mcp ucu (/mcp-admin öldü; kampanya = admin işi, anonim set
// YOK). Oturum başına taze options; tool seti token scope'una göre budanır — scope yoksa boş liste.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, mcpOptions, _) =>
    {
        var tools = mcpOptions.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => !McpScopePruningExtension.IsToolVisible(
                         t.ProtocolTool.Name, Discount.Api.Mcp.DiscountAdminSurface.ToolScopeMap, ctx.User)).ToArray())
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

// 085 R5: korumalı yönetim ucu — kimliksiz istek 401 + resource_metadata challenge. /mcp-admin öldü.
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();
