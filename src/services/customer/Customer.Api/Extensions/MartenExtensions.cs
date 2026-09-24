namespace Customer.Api.Extensions;

// Customer kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddCustomerMarten(this WebApplicationBuilder builder)
    {
        var customerDb = builder.Configuration.GetConnectionString("customerDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.CustomerSchemaName;
                opts.Connection(customerDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
                // 076: Wallet (kart-saklama) SÖKÜLDÜ; Customer BC = AddressBook + MerchantInformation.
                opts.Schema.For<Customer.Api.Domains.AddressBooks.AddressBook>().Index(x => x.UserId);
                // Merchant kimliği (tekil kayıt) — merchant onboarding/admin.
                opts.Schema.For<Customer.Api.Domains.MerchantInformations.MerchantInformation>();
                // 078: tek kullanımlık credential-giriş ekran oturumu — token'la yüklenir.
                opts.Schema.For<Customer.Api.Domains.MerchantInformations.CredentialEntrySession>()
                    .Index(x => x.Token);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
