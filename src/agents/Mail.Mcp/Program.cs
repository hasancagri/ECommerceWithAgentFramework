var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// SMTP hedefi ZORUNLU — acilista fail-fast (Host/Port AppHost'tan Mailpit endpoint'iyle gelir).
builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration(SmtpOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<SmtpOptions>(sp =>
    sp.GetRequiredService<IOptions<SmtpOptions>>().Value);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMcp("/mcp");
await app.RunAsync();