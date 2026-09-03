using OpenIddict.Server;

namespace Identity.Server.Connect;

// 061: OpenIddict 7.6'da DCR built-in değil — discovery dokümanına registration_endpoint
// alanını elle ekleriz. Claude Code DCR ucunu yalnız bu alandan bulur (R2).
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

        return default;
    }
}