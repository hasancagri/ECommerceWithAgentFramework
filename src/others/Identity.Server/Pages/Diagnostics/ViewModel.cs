using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;

namespace Identity.Server.Pages.Diagnostics;

public class ViewModel
{
    public ViewModel(AuthenticateResult result)
    {
        AuthenticateResult = result;

        if (result?.Properties?.Items.TryGetValue("client_list", out var encoded) == true)
        {
            if (encoded != null)
            {
                var bytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(encoded);
                var value = Encoding.UTF8.GetString(bytes);
                Clients = JsonSerializer.Deserialize<string[]>(value) ?? Enumerable.Empty<string>();
                return;
            }
        }
        Clients = Enumerable.Empty<string>();
    }

    public AuthenticateResult AuthenticateResult { get; }
    public IEnumerable<string> Clients { get; }
}
