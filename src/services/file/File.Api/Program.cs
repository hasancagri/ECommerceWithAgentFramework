var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcı depo kökü ZORUNLU — açılışta fail-fast (RootPath AppHost env'inden gelir).
builder.Services.AddOptions<CoverStoreOptions>()
    .BindConfiguration(CoverStoreOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<CoverStoreOptions>(sp =>
    sp.GetRequiredService<IOptions<CoverStoreOptions>>().Value);

builder.Services.AddOptions<CoverMigrationOptions>()
    .BindConfiguration(CoverMigrationOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<CoverMigrationOptions>(sp =>
    sp.GetRequiredService<IOptions<CoverMigrationOptions>>().Value);

// IFileStore (LocalDiskFileStore) marker ile otomatik kaydedilir (AsImplementedInterfaces).
builder.Services.AddAllDependencies();
// XlsxCoverSource somut tipiyle enjekte edilir (arayüzsüz stateless helper) → elle kayıt.
builder.Services.AddSingleton<XlsxCoverSource>();

// Migration görsel indirme HttpClient'ı. Hosted service her zaman kayıtlı; Enabled=false ise erken döner.
builder.Services.AddHttpClient();
builder.Services.AddHostedService<CoverMigrationHostedService>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapCoverEndpoints();   // GET /files/v1/covers/{isbn} (anonim)
await app.RunAsync();
