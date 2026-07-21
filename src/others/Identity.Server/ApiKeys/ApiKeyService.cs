using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Server;

// Anahtar üretimi (umk_ + 32 rastgele bayt), SHA-256 hash, hash ile çözümleme + UserScopes okuma.
public class ApiKeyService(ApplicationDbContext db, UserManager<ApplicationUser> users)
{
    public const string Prefix = "umk_";

    // Yüksek entropili rastgele anahtar → tahmin/türetme yok. Ham değer yalnızca bir kez döner.
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var body = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Prefix + body;
    }

    // Yüksek entropili olduğu için parola-hash gerekmez; SHA-256 yeterli ve hızlı.
    public static string Hash(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public async Task<(ApiKey Entity, string RawKey)> IssueAsync(string userId, string? name, CancellationToken ct)
    {
        var raw = Generate();
        var entity = ApiKey.Create(userId, Hash(raw), name);
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);
        return (entity, raw);
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return false;

        entity.Revoke();
        await db.SaveChangesAsync(ct);
        return true;
    }

    // Ham anahtardan kullanıcı + UserScopes çözümler. Yok/iptalli/kullanıcısız → null (handler Fail() sayar).
    public async Task<ResolvedKey?> ResolveAsync(string rawKey, CancellationToken ct)
    {
        var hash = Hash(rawKey);
        var entity = await db.ApiKeys.FirstOrDefaultAsync(x => x.KeyHash == hash && !x.IsRevoked, ct);
        if (entity is null)
            return null;

        var user = await users.FindByIdAsync(entity.UserId);
        if (user is null)
            return null;

        var scopes = await db.UserScopes
            .Where(x => x.UserId == entity.UserId)
            .Select(x => x.Scope)
            .ToArrayAsync(ct);

        return new ResolvedKey(user.Id, user.Email, scopes);
    }
}

// Resolve çıktısı (kalıcı değil). Handler bundan ClaimsPrincipal kurar.
public record ResolvedKey(string UserId, string? Email, string[] Scopes);