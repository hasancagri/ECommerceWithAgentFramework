namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// 045 US2: bir üyenin varyant ailesini döner (seçicinin tek veri kaynağı). Ailesiz/tek görünür
// üyeli ürün TEK üye + boş eksen döner → WebApp seçici çizmez. Üye filtresi = dolu-satır (liste/detayla aynı).
public static class GetProductFamily
{
    public record GetProductFamilyQuery(Guid ProductId);

    public class VariantAxis
    {
        public string Attribute { get; set; } = null!;
        public IReadOnlyList<string> Options { get; set; } = [];
    }

    public class FamilyMember
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsInStock { get; set; }
        public bool IsCurrent { get; set; }
        public List<SpecPairResponse> Specs { get; set; } = [];
    }

    public class SpecPairResponse
    {
        public string Attribute { get; set; } = null!;
        public string Option { get; set; } = null!;
    }

    public class GetProductFamilyResponse
    {
        public string? FamilyCode { get; set; }
        public List<VariantAxis> Axes { get; set; } = [];
        public List<FamilyMember> Members { get; set; } = [];
    }

    // Saf çekirdek (test-first): eksen = üyeler arasında ≥2 FARKLI MEVCUT değeri olan attribute.
    // Tek mevcut değer (kalanı eksik) anlamlı seçim vermez → eksen DEĞİL (enrich'in asimetrik
    // doldurduğu tek-değerli attribute'lar seçici gürültüsü yapmasın). Options mevcut değerler.
    public static IReadOnlyList<VariantAxis> DeriveAxes(IReadOnlyList<StorefrontView> members)
    {
        var attributes = members.SelectMany(m => m.Specs.Select(s => s.Attribute)).Distinct();
        var axes = new List<VariantAxis>();
        foreach (var attribute in attributes.OrderBy(a => a, StringComparer.Ordinal))
        {
            var presentOptions = members
                .Select(m => m.Specs.FirstOrDefault(s => s.Attribute == attribute)?.Option)
                .Where(o => o is not null)
                .Distinct()
                .OrderBy(o => o, StringComparer.Ordinal)
                .ToList();
            if (presentOptions.Count < 2)
                continue; // tek/sıfır seçenek → seçilecek varyant yok, eksen değil

            axes.Add(new VariantAxis { Attribute = attribute, Options = presentOptions! });
        }

        return axes;
    }

    public class GetProductFamilyQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetProductFamilyResponse>> Handle(
            GetProductFamilyQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var current = await session.LoadAsync<StorefrontView>(query.ProductId, ct);
            if (current is null || current.IsDeleted || current.Name is null || current.Price is null)
                return FeatureObjectResultModel<GetProductFamilyResponse>.NotFound();

            // Ailesiz veya tek üye: kendisini tek üye olarak döner, eksen yok (WebApp seçici çizmez).
            IReadOnlyList<StorefrontView> members;
            if (string.IsNullOrWhiteSpace(current.FamilyCode))
            {
                members = [current];
            }
            else
            {
                members = await session.Query<StorefrontView>()
                    .Where(x => x.FamilyCode == current.FamilyCode
                                && !x.IsDeleted && x.Name != null && x.Price != null)
                    .ToListAsync(ct);
            }

            var axes = DeriveAxes(members);
            var response = new GetProductFamilyResponse
            {
                FamilyCode = current.FamilyCode,
                Axes = axes.ToList(),
                Members = members
                    .OrderBy(m => m.Name)
                    .Select(m => new FamilyMember
                    {
                        ProductId = m.ProductId,
                        Name = m.Name!,
                        Price = m.Price!.Value,
                        ImageUrl = m.ImageUrl,
                        IsInStock = m.StockQuantity is > 0,
                        IsCurrent = m.ProductId == current.ProductId,
                        Specs = m.Specs.Select(s => new SpecPairResponse
                        { Attribute = s.Attribute, Option = s.Option }).ToList(),
                    }).ToList(),
            };

            return FeatureObjectResultModel<GetProductFamilyResponse>.Ok(response);
        }
    }
}

public static class GetProductFamilyEndpoint
{
    public static RouteGroupBuilder GetProductFamilyGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // FR-011: anonim okuma (girişsiz dahil) — mevcut storefront uçlarıyla aynı.
        group.MapGet("/{productId:guid}/family", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProductFamily.GetProductFamilyResponse>>(
                    new GetProductFamily.GetProductFamilyQuery(productId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductFamily")
            .MapToApiVersion(1, 0);
        return group;
    }
}
