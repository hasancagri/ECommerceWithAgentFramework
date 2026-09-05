namespace Identity.Server.Connect;

// 061: OpenIddict 7.6'da DCR built-in değil — discovery dokümanına registration_endpoint
// alanını elle ekleriz. Claude Code DCR ucunu yalnız bu alandan bulur (R2).
// Ayrıca token_endpoint_auth_methods_supported'a "none" eklenir: DCR istemcileri public+PKCE'dir;
// "none" ilan edilmezse standart istemciler (ör. mcp-remote) secret'lı yöntemle kayıt olmaya
// çalışır ve DCR validator'ı bunları invalid_client_metadata ile reddeder.
public sealed class RegistrationEndpointMetadataHandler
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleConfigurationRequestContext>()
            .UseSingletonHandler<RegistrationEndpointMetadataHandler>()
            .SetOrder(int.MaxValue - 100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictServerEvents.HandleConfigurationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var issuer = context.Options.Issuer
            ?? throw new InvalidOperationException("Issuer tanımlı olmalı.");

        context.Metadata["registration_endpoint"] =
            new Uri(issuer, "connect/register").AbsoluteUri;

        context.TokenEndpointAuthenticationMethods.Add(ClientAuthenticationMethods.None);

        return default;
    }
}