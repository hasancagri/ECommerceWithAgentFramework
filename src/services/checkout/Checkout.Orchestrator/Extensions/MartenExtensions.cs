namespace Checkout.Orchestrator.Extensions;

// Checkout kalıcılık kurulumu: Marten (Postgres document store) + Wolverine entegrasyonu.
// Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddCheckoutMarten(this WebApplicationBuilder builder)
    {
        var checkoutDb = builder.Configuration.GetConnectionString("checkoutDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.CheckoutSchemaName;
                opts.Connection(checkoutDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s =>
                    {
                        s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
                    });
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
