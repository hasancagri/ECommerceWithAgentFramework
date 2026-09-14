using Common.Utils.Authorization;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Http;
using Wolverine;
using Basket.Api.Domains.Baskets.Features.Commands;

namespace Basket.Api.Tests;

// 077 bug #7 regresyon: checkout saga'nin ClearBasket adimi Basket'in broker handler'indan
// (BasketEventHandlers, HttpContext YOK) IMessageBus.InvokeAsync ile cagrilir. Komut uzerinde
// [RequiredScope] varsa ScopeAuthorizationMiddleware.Before, HttpContext null oldugundan
// UnauthorizedAccessException firlatir -> handler firlar -> BasketCleared reply cascade olmaz ->
// saga ClearingBasket'te takilir. Fix: attribute kaldirildi (ic komut, guard yuzeydeki gRPC ucunda).
// Bu test hem guard mekanizmasini (scope'lu mesaj HttpContext'siz atar) hem fix'i (bu komut atmaz) kilitler.
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
    public void ClearBasketByCheckout_HasNoScopeAttribute_SoBrokerPathDoesNotThrow()
    {
        var command = new ClearBasketByCheckout.ClearBasketByCheckoutCommand(Guid.NewGuid(), Guid.NewGuid());
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
