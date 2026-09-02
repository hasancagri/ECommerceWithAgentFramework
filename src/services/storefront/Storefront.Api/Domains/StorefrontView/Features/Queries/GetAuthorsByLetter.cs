namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// Yazar dizini harf dilimi: harf seçilince yalnız o harfle başlayan yazarlar döner. Yazar çok-değerli
// jsonb koleksiyon (Authors) — satır daraltması DB'de jsonb_array_elements EXISTS ile, ad ayıklama +
// tekilleştirme bellekte (satır o harfe daralmıştır; çok-yazarlı kitabın harf-dışı yazarı elenir).
// Cache "filters" tag'ini paylaşır (ürün değişiminde boşalır) + 60sn TTL güvenlik ağı.
public static class GetAuthorsByLetter
{
    [Cached("filters", 60)]
    public record GetAuthorsByLetterQuery(string Letter);

    public class AuthorIndexItemResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }

    // Saf, test edilebilir çekirdek: "A".."Z" veya "#" (harf-dışı) — büyük harfe normalize edilir,
    // geçersiz girdi null (çağıran INVALID_VALUE döner).
    public static string? NormalizeLetter(string? letter)
    {
        var t = letter?.Trim().ToUpperInvariant();
        if (t is not { Length: 1 }) return null;
        return t[0] is (>= 'A' and <= 'Z') or '#' ? t : null;
    }

    // Saf çekirdek: SQL tarafındaki eşleşmenin bellek-içi ikizi ("#" = A-Z ile başlamayan).
    public static bool MatchesLetter(string? name, string letter)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var c = char.ToUpperInvariant(name[0]);
        var isAz = c is >= 'A' and <= 'Z';
        return letter == "#" ? !isAz : c == letter[0];
    }

    public class GetAuthorsByLetterQueryHandler
    {
        public async Task<FeatureListResultModel<AuthorIndexItemResponse>> Handle(
            GetAuthorsByLetterQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var letter = NormalizeLetter(query.Letter);
            if (letter is null)
                return FeatureListResultModel<AuthorIndexItemResponse>.Error(
                    new MessageItem { Code = StorefrontResourceConstants.INVALID_VALUE });

            // Liste ile aynı satılabilirlik (dolu-satır) filtresi; harf daraltması DB'de
            // (jsonb dizi elemanı üstünde ILIKE / regex — LINQ iç-içe Any+StartsWith çevirisi yok, 040 dersi).
            var sellable = session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null);

            sellable = letter == "#"
                ? sellable.Where(x => x.MatchesSql(
                    "EXISTS (SELECT 1 FROM jsonb_array_elements(d.data -> 'Authors') a WHERE a ->> 'Name' !~* '^[a-z]')"))
                : sellable.Where(x => x.MatchesSql(
                    "EXISTS (SELECT 1 FROM jsonb_array_elements(d.data -> 'Authors') a WHERE a ->> 'Name' ILIKE ?)",
                    letter + "%"));

            var rows = await sellable.ToListAsync(ct);

            var authors = rows
                .SelectMany(x => x.Authors)
                .Where(a => MatchesLetter(a.Name, letter))
                .GroupBy(a => a.Id)
                .Select(g => new AuthorIndexItemResponse { Id = g.Key, Name = g.First().Name })
                .OrderBy(x => x.Name)
                .ToList();

            return FeatureListResultModel<AuthorIndexItemResponse>.Ok(authors);
        }
    }
}

public static class GetAuthorsByLetterEndpoint
{
    public static RouteGroupBuilder GetAuthorsByLetterGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/authors", async (IMessageBus bus, string letter) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetAuthorsByLetter.AuthorIndexItemResponse>>(
                    new GetAuthorsByLetter.GetAuthorsByLetterQuery(letter));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetAuthorsByLetter")
            .MapToApiVersion(1, 0)
            .Produces<FeatureListResultModel<GetAuthorsByLetter.AuthorIndexItemResponse>>()
            .AllowAnonymous();

        return group;
    }
}