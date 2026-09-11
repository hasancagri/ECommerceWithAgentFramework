var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddOptions<UcpSimOptions>().BindConfiguration(nameof(UcpSimOptions));
builder.Services.AddSingleton<UcpSimOptions>(sp => sp.GetRequiredService<IOptions<UcpSimOptions>>().Value);

// Identity token istemcisi (dev self-signed sertifikayı kabul et).
builder.Services.AddHttpClient("identity")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// Mağaza /ucp cephesi istemcisi (service discovery ServiceDefaults'tan; dev self-signed kabul).
builder.Services.AddHttpClient<UcpStoreClient>((sp, c) =>
    {
        c.BaseAddress = new Uri(sp.GetRequiredService<UcpSimOptions>().StoreBaseUrl.TrimEnd('/') + "/");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// Simülatör = MCP server (Claude Desktop bağlanır); tool'lar mağaza checkout uçlarını çağırır.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();

// Anonim MCP (simülatör dış platformu canlandırır; kendi yetkisi mağazaya client_credentials ile).
app.MapMcp("/mcp");

// US3: mağazadan gelen imzalı sipariş-olayı webhook'unu alır + imzayı doğrular.
app.MapWebhookInbox();

await app.RunAsync();