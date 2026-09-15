namespace Catalog.Api.Domains.ProductTags.Features.Agents.Commands;

// 070 parite: etiket OLUŞTURMA (agent yüzeyi) — CreateProductTag İKİZİ (bilinçli tekrar). Etiket dış
// kontrat taşımaz (Storefront/event yok) — yalnız Catalog içi kayıt.
public static class AdminCreateProductTag
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminCreateProductTagCommand(Guid UserId, string Name);

    public class AdminCreateProductTagResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminCreateProductTagCommandHandler
    {
        public Task<FeatureObjectResultModel<AdminCreateProductTagResponse>> Handle(
            AdminCreateProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Ad zorunluluğu handler'da (factory düz aggregate döner — ProductTag.Create notu).
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return Task.FromResult(FeatureObjectResultModel<AdminCreateProductTagResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.TAG_NAME_REQUIRED
                }));

            var tag = ProductTag.Create(cmd.Name);
            session.Store(tag);

            return Task.FromResult(FeatureObjectResultModel<AdminCreateProductTagResponse>.Ok(
                new AdminCreateProductTagResponse { Id = tag.Id, Name = tag.Name }));
        }
    }
}

// 070: ADMIN tool'lari — YALNIZ korumali /mcp-admin ucunda yayinlanir (Program.cs oturum filtresi);
// anonim /mcp kesif seti DEGISMEZ. Kullanici token'dan (ICurrentUser); scope katmani handler'da
// [RequiredScope(ProductTagCreate)].
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).
[McpServerToolType]
public static class AdminCreateProductTagMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateProductTag)]
    [Description(
        "YONETIM/YAZMA: yeni bir urun etiketi olusturur (or. 'yeni-sezon', 'outlet'). Ad zorunludur. " +
        "Yanit {id, name} — olusturulan etiketin kimligi ve adi. Islem denetim izine kaydedilir.")]
    public static Task<FeatureObjectResultModel<AdminCreateProductTag.AdminCreateProductTagResponse>> AdminCreateProductTagAsync(
        [Description("Yeni etiket adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminCreateProductTag.AdminCreateProductTagResponse>>(
            new AdminCreateProductTag.AdminCreateProductTagCommand(userId, name), ct);
    }
}
