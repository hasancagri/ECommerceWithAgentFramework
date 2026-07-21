namespace Identity.Server.Tests;

public class ApiKeyServiceTests
{
    [Fact]
    public void Hash_IsDeterministic()
    {
        ApiKeyService.Hash("umk_abc123").ShouldBe(ApiKeyService.Hash("umk_abc123"));
    }

    [Fact]
    public void Hash_DifferentInput_DifferentHash()
    {
        ApiKeyService.Hash("umk_aaa").ShouldNotBe(ApiKeyService.Hash("umk_bbb"));
    }

    [Fact]
    public void Hash_IsSha256Hex_64Chars()
    {
        var hash = ApiKeyService.Hash("umk_abc123");

        hash.Length.ShouldBe(64);
        hash.ShouldBe(hash.ToLowerInvariant());
    }

    [Fact]
    public void Generate_HasPrefix()
    {
        ApiKeyService.Generate().ShouldStartWith(ApiKeyService.Prefix);
    }

    [Fact]
    public void Generate_ProducesUniqueKeys()
    {
        ApiKeyService.Generate().ShouldNotBe(ApiKeyService.Generate());
    }

    [Fact]
    public void Generate_TamperedKey_HashesDifferently()
    {
        // SC-005: tek karakter degisince baska kullaniciya cozumlenmez (farkli hash).
        var key = ApiKeyService.Generate();
        var tampered = key[..^1] + (key[^1] == 'a' ? 'b' : 'a');

        ApiKeyService.Hash(tampered).ShouldNotBe(ApiKeyService.Hash(key));
    }
}