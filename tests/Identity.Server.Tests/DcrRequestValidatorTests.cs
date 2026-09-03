using Identity.Server.Connect;
using Shouldly;
using Xunit;

namespace Identity.Server.Tests;

// 061: RFC 7591 DCR istek doğrulayıcısı (saf birim, test-first — İlke VI).
// Kurallar: specs/061-external-mcp-oauth/contracts/dcr-register.md
public class DcrRequestValidatorTests
{
    private static DcrRequest Valid(
        string[]? redirectUris = null,
        string[]? grantTypes = null,
        string? authMethod = "none",
        string? scope = null) =>
        new(
            ClientName: "Claude Code",
            RedirectUris: redirectUris ?? ["http://localhost:33418/callback"],
            GrantTypes: grantTypes,
            TokenEndpointAuthMethod: authMethod,
            Scope: scope);

    // --- redirect_uris ---

    [Theory]
    [InlineData("http://localhost:33418/callback")]
    [InlineData("http://localhost:8080/auth/redirect")]
    [InlineData("http://127.0.0.1:41337/callback")]
    [InlineData("https://claude.ai/api/mcp/auth_callback")]
    [InlineData("https://claude.com/api/mcp/auth_callback")]
    public void Accepts_allowed_redirect_uri(string uri)
    {
        var result = DcrRequestValidator.Validate(Valid(redirectUris: [uri]));

        result.IsValid.ShouldBeTrue();
        result.RedirectUris.ShouldBe([uri]);
    }

    [Theory]
    [InlineData("https://evil.com/callback")]
    [InlineData("http://localhost.evil.com:8080/callback")]
    [InlineData("https://claude.ai/api/mcp/other")]
    [InlineData("https://claude.ai.evil.com/api/mcp/auth_callback")]
    [InlineData("https://localhost:5001/callback")]
    [InlineData("ftp://localhost:21/callback")]
    [InlineData("not-a-uri")]
    public void Rejects_disallowed_redirect_uri(string uri)
    {
        var result = DcrRequestValidator.Validate(Valid(redirectUris: [uri]));

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("invalid_redirect_uri");
    }

    [Fact]
    public void Rejects_missing_redirect_uris()
    {
        var result = DcrRequestValidator.Validate(Valid() with { RedirectUris = null });

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("invalid_redirect_uri");
    }

    [Fact]
    public void Rejects_empty_redirect_uris()
    {
        var result = DcrRequestValidator.Validate(Valid(redirectUris: []));

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("invalid_redirect_uri");
    }

    [Fact]
    public void Rejects_when_any_redirect_uri_is_disallowed()
    {
        var result = DcrRequestValidator.Validate(
            Valid(redirectUris: ["http://localhost:1234/cb", "https://evil.com/cb"]));

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("invalid_redirect_uri");
    }

    // --- grant_types ---

    [Fact]
    public void Defaults_grant_types_when_missing()
    {
        var result = DcrRequestValidator.Validate(Valid(grantTypes: null));

        result.IsValid.ShouldBeTrue();
        result.GrantTypes.ShouldBe(["authorization_code", "refresh_token"]);
    }

    [Fact]
    public void Keeps_authorization_code_only_subset()
    {
        var result = DcrRequestValidator.Validate(Valid(grantTypes: ["authorization_code"]));

        result.IsValid.ShouldBeTrue();
        result.GrantTypes.ShouldBe(["authorization_code"]);
    }

    [Theory]
    [InlineData("client_credentials")]
    [InlineData("implicit")]
    [InlineData("password")]
    public void Rejects_disallowed_grant_type(string grant)
    {
        var result = DcrRequestValidator.Validate(
            Valid(grantTypes: ["authorization_code", grant]));

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("invalid_client_metadata");
    }

    // --- token_endpoint_auth_method ---

    [Fact]
    public void Accepts_auth_method_none()
    {
        DcrRequestValidator.Validate(Valid(authMethod: "none")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_missing_auth_method_as_none()
    {
        DcrRequestValidator.Validate(Valid(authMethod: null)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("client_secret_basic")]
    [InlineData("client_secret_post")]
    [InlineData("private_key_jwt")]
    public void Rejects_confidential_auth_method(string method)
    {
        var result = DcrRequestValidator.Validate(Valid(authMethod: method));

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("invalid_client_metadata");
    }

    // --- scope ---

    [Fact]
    public void Assigns_full_bundle_when_scope_missing()
    {
        var result = DcrRequestValidator.Validate(Valid(scope: null));

        result.IsValid.ShouldBeTrue();
        result.Scopes.ShouldBe(ExternalAgentDefaults.AllScopes, ignoreOrder: true);
    }

    [Fact]
    public void Intersects_requested_scope_with_bundle()
    {
        var result = DcrRequestValidator.Validate(Valid(scope: "openid basket.read basket.write"));

        result.IsValid.ShouldBeTrue();
        result.Scopes.ShouldBe(["openid", "basket.read", "basket.write"], ignoreOrder: true);
    }

    [Fact]
    public void Silently_drops_out_of_bundle_scopes()
    {
        var result = DcrRequestValidator.Validate(
            Valid(scope: "basket.read catalog.write identity.roles.manage stock.write"));

        result.IsValid.ShouldBeTrue();
        result.Scopes.ShouldBe(["basket.read"]);
    }
}