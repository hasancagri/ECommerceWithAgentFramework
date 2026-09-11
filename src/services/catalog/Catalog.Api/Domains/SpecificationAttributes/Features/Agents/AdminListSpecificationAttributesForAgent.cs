namespace Catalog.Api.Domains.SpecificationAttributes.Features.Agents;

// 074: REST admin yüzeyi söküldü — GetSpecificationAttributes İKİZİ (bilinçli tekrar) agent yüzeyi.
// Okuma slice: scope/audit YOK. Tüm özellik tanımları + kapalı-liste seçenekleri (043), DisplayOrder sıralı.
public static class AdminListSpecificationAttributesForAgent
{
    public record ListSpecificationAttributesQuery;

    public class OptionItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int DisplayOrder { get; set; }
    }

    public class SpecificationAttributeItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public bool Filterable { get; set; }
        public int DisplayOrder { get; set; }
        public List<OptionItem> Options { get; set; } = [];
    }

    public class ListSpecificationAttributesQueryHandler
    {
        public async Task<FeatureListResultModel<SpecificationAttributeItem>> Handle(
            ListSpecificationAttributesQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var attributes = await session.Query<SpecificationAttribute>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.DisplayOrder)
                .ToListAsync(ct);

            return FeatureListResultModel<SpecificationAttributeItem>.Ok(
                attributes.Select(a => new SpecificationAttributeItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    Filterable = a.Filterable,
                    DisplayOrder = a.DisplayOrder,
                    Options = a.Options.OrderBy(o => o.DisplayOrder)
                        .Select(o => new OptionItem { Id = o.Id, Name = o.Name, DisplayOrder = o.DisplayOrder })
                        .ToList(),
                }).ToList());
        }
    }
}
