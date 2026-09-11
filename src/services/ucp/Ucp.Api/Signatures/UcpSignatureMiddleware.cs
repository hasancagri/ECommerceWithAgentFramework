using Microsoft.AspNetCore.Http.Extensions;

namespace Ucp.Api.Signatures;

/// <summary>
/// 072 US4: gelen checkout isteklerinde RFC 9421 imza doğrulaması. Zorlama <c>RequireSignatures</c>
/// bayrağına bağlı (FR-011): açık → imzasız/bozuk 401; kapalı (sandbox default) → imza varsa doğrula
/// ama akış BLOKE olmaz (log). Yalnız /ucp/checkout_sessions yazma uçlarına uygulanır; keşif/katalog anonim.
/// </summary>
public sealed class UcpSignatureMiddleware(RequestDelegate next, UcpSigningOption signing, ILogger<UcpSignatureMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, HttpMessageSignatureVerifier verifier)
    {
        var path = context.Request.Path;
        var isCheckout = path.StartsWithSegments("/ucp/checkout_sessions") && HttpMethods.IsPost(context.Request.Method);

        if (!isCheckout)
        {
            await next(context);
            return;
        }

        // Gövdeyi buffer'la (verifier + endpoint ikisi de okur).
        context.Request.EnableBuffering();
        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();
        context.Request.Body.Position = 0;

        var req = context.Request;
        var result = verifier.Verify(
            req.Method,
            req.GetDisplayUrl(),
            req.Headers["Signature-Input"].FirstOrDefault(),
            req.Headers["Signature"].FirstOrDefault(),
            req.Headers["Content-Digest"].FirstOrDefault(),
            body,
            DateTimeOffset.UtcNow);

        if (signing.RequireSignatures)
        {
            if (result != SignatureVerification.Valid)
            {
                logger.LogWarning("UCP imza zorlaması: istek reddedildi ({Result}) {Path}", result, path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = UcpResourceConstants.SIGNATURE_INVALID, detail = result.ToString() });
                return;
            }
        }
        else if (result == SignatureVerification.Invalid)
        {
            // Sandbox: bozuk imza bloke etmez ama loglanır (gözlemlenebilirlik).
            logger.LogInformation("UCP imza (zorlama kapalı): bozuk imza gözlemlendi {Path}", path);
        }

        await next(context);
    }
}
