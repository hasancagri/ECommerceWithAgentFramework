namespace Identity.Server.Tests;

public class ApiKeyTests
{
    [Fact]
    public void Create_NewKey_IsActiveWithHashAndUser()
    {
        var key = ApiKey.Create("user-1", "hash-1", "n8n");

        key.Id.ShouldNotBe(Guid.Empty);
        key.UserId.ShouldBe("user-1");
        key.KeyHash.ShouldBe("hash-1");
        key.Name.ShouldBe("n8n");
        key.IsRevoked.ShouldBeFalse();
        key.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void Revoke_SetsFlagAndTimestamp()
    {
        var key = ApiKey.Create("user-1", "hash-1", null);

        key.Revoke();

        key.IsRevoked.ShouldBeTrue();
        key.RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Revoke_IsIdempotent_KeepsFirstTimestamp()
    {
        var key = ApiKey.Create("user-1", "hash-1", null);

        key.Revoke();
        var firstRevokedAt = key.RevokedAt;
        key.Revoke();

        key.IsRevoked.ShouldBeTrue();
        key.RevokedAt.ShouldBe(firstRevokedAt);
    }
}