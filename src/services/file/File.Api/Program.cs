using Amazon.S3;

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

builder.Services.AddOptions<R2Options>()
    .BindConfiguration(R2Options.SectionName);
builder.Services.AddSingleton<R2Options>(sp =>
    sp.GetRequiredService<IOptions<R2Options>>().Value);

builder.Services.AddAllDependencies();
// XlsxCoverSource somut tipiyle enjekte edilir (arayüzsüz stateless helper) → elle kayıt.
builder.Services.AddSingleton<XlsxCoverSource>();

// R2 S3 client — yalnız S3FileStore çözülünce inşa edilir (lazy). Endpoint AccountId'den,
// credential user-secrets'ten. AuthenticationRegion="auto" (R2 gereği).
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var o = sp.GetRequiredService<R2Options>();
    var config = new AmazonS3Config
    {
        ServiceURL = o.ServiceUrl,
        ForcePathStyle = true,
        AuthenticationRegion = "auto"
    };
    return new AmazonS3Client(o.AccessKeyId, o.SecretAccessKey, config);
});

// Backend seçimi bound Options'tan (IConfiguration doğrudan okuma YOK). İki impl aynı IFileStore
// arayüzünü paylaşır → factory ile birini seç; seçilmeyen inşa edilmez (S3 client de lazy).
builder.Services.AddSingleton<LocalDiskFileStore>();
builder.Services.AddSingleton<S3FileStore>();
builder.Services.AddSingleton<IFileStore>(sp =>
{
    var store = sp.GetRequiredService<CoverStoreOptions>();
    return store.Backend == CoverStoreBackend.R2
        ? sp.GetRequiredService<S3FileStore>()
        : sp.GetRequiredService<LocalDiskFileStore>();
});

// xlsx-download migration (yerel disk doldurma). HttpClient + hosted service her zaman kayıtlı;
// Enabled=false ise erken döner.
builder.Services.AddHttpClient();
builder.Services.AddHostedService<CoverMigrationHostedService>();
// Yerel disk → R2 kopyalama. Her zaman kayıtlı; SyncLocalToR2=false / Backend!=R2 ise erken döner.
builder.Services.AddHostedService<R2SyncHostedService>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapCoverEndpoints();   // GET /files/v1/covers/{isbn} (anonim)
await app.RunAsync();