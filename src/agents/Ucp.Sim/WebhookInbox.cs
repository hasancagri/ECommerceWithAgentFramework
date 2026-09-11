using System.Text;
using System.Text.RegularExpressions;

namespace Ucp.Sim;

/// <summary>
/// 072 US3: simülatörün webhook alıcısı — mağazanın imzalı sipariş-olayı bildirimini (order.confirmed/
/// order.canceled) alır ve RFC 9421 imzasını (ES256) mağaza public key'iyle doğrular. Test kanıtı:
/// gelen imza geçerli mi. Sonucu loglar, 200 döner (teslim başarılı sayılır).
/// </summary>
public static class WebhookInbox
{
    private static readonly Regex CreatedRx = new(@"created=(\d+)", RegexOptions.Compiled);
    private static readonly Regex KeyIdRx = new("keyid=\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex AlgRx = new("alg=\"([^\"]+)\"", RegexOptions.Compiled);

    public static void MapWebhookInbox(this WebApplication app)
    {
        app.MapPost("/inbox", async (HttpRequest request, UcpSimOptions opts, ILoggerFactory lf) =>
        {
            var logger = lf.CreateLogger("UcpSim.WebhookInbox");

            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms);
            var body = ms.ToArray();

            var verdict = Verify(request, body, opts);
            logger.LogInformation("UCP webhook alındı — imza: {Verdict}. Gövde: {Body}",
                verdict, Encoding.UTF8.GetString(body));

            return Results.Ok(new { received = true, signature = verdict });
        });
    }

    private static string Verify(HttpRequest request, byte[] body, UcpSimOptions opts)
    {
        var input = request.Headers["Signature-Input"].FirstOrDefault();
        var signature = request.Headers["Signature"].FirstOrDefault();
        var contentDigest = request.Headers["Content-Digest"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(contentDigest))
            return "missing";
        if (!string.Equals(contentDigest.Trim(), ContentDigest.Compute(body), StringComparison.Ordinal))
            return "invalid-digest";
        if (string.IsNullOrWhiteSpace(opts.StorePublicKeyPem))
            return "no-key";

        var createdM = CreatedRx.Match(input);
        var keyIdM = KeyIdRx.Match(input);
        var algM = AlgRx.Match(input);
        if (!createdM.Success || !keyIdM.Success || !algM.Success) return "invalid-input";
        if (keyIdM.Groups[1].Value != opts.StoreKeyId) return "unknown-kid";

        var created = long.Parse(createdM.Groups[1].Value);
        var paramsValue = SignatureBaseBuilder.BuildParams(created, keyIdM.Groups[1].Value, algM.Groups[1].Value);
        var baseStr = SignatureBaseBuilder.Build("POST", opts.InboxUrl, contentDigest.Trim(), paramsValue);

        var start = signature.IndexOf(':'); var end = signature.LastIndexOf(':');
        if (start < 0 || end <= start) return "invalid-sig";
        var sigBytes = Convert.FromBase64String(signature.Substring(start + 1, end - start - 1));

        return Es256Signature.Verify(baseStr, sigBytes, opts.StorePublicKeyPem) ? "valid" : "invalid";
    }
}
