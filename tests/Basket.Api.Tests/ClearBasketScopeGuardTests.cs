using Common.Utils.Authorization;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Http;
using Wolverine;
using static Shared.CheckoutMessages;

namespace Basket.Api.Tests;

// 077 bug #7 regresyon: checkout sağasının ClearBasket adımı Basket'in broker handler'ından
// (Saga.CheckoutConsumers, HttpContext YOK) doğrudan tetiklenir. Komut üzerinde [RequiredScope] varsa
// ScopeAuthorizationMiddleware.Before, HttpContext null olduğundan UnauthorizedAccessException fırlatır
// -> handler fırlar -> BasketCleared reply cascade olmaz -> saga ClearingBasket'te takılır.
// 074: ara ClearBasketByCheckoutCommand (iç komut) kaldırıldı — Saga aggregate'e doğrudan dokunuyor,
// tek dispatch katmanı kaldı. Guard artık doğrudan broker komutu ClearBasketCommand üzerinde.
public class ClearBasketScopeGuardTests
{
    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    // Kontrast probu: [RequiredScope] tasiyan bir mesaj HttpContext yoksa engellenmeli.
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    private sealed record ScopedProbe;

    [Fact]
    public void ClearBasketCommand_HasNoScopeAttribute_SoBrokerPathDoesNotThrow()
    {
        var command = new ClearBasketCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString());
        var envelope = new Envelope(command);

        Should.NotThrow(() => ScopeAuthorizationMiddleware.Before(envelope, new NullHttpContextAccessor()));
    }

    [Fact]
    public void ScopedMessage_WithoutHttpContext_ThrowsUnauthorized()
    {
        var envelope = new Envelope(new ScopedProbe());

        Should.Throw<UnauthorizedAccessException>(
            () => ScopeAuthorizationMiddleware.Before(envelope, new NullHttpContextAccessor()));
    }
}