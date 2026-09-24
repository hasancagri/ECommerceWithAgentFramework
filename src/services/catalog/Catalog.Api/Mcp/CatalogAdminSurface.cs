using Common.Utils.Constants;

namespace Catalog.Api.Mcp;

// 070/074/083: catalog /mcp-admin yönetim yüzeyi politikası (Program.cs orkestrasyon dışı tutulur).
public static class CatalogAdminSurface
{
    // /mcp-admin ucunda GÖRÜNECEK admin tool'ları (anonim /mcp'de budanır). ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya EKLE — ad-prefix DEĞİL açık liste; eksik tool /mcp-admin'de kaybolur (074 dersi).
    public static readonly string[] ToolNames =
    [
        Shared.CatalogAdminTools.ListProducts, Shared.CatalogAdminTools.GetProduct,
        Shared.CatalogAdminTools.UpdateProduct, Shared.CatalogAdminTools.SetPublished,
        Shared.CatalogAdminTools.GetPriceHistory,
        // 074: parite tool'ları (REST admin söküldü) — hepsi YALNIZ /mcp-admin'de.
        Shared.CatalogAdminTools.CreateProduct, Shared.CatalogAdminTools.SetProductDimensions,
        Shared.CatalogAdminTools.SetProductSeo, Shared.CatalogAdminTools.AssignProductTag,
        Shared.CatalogAdminTools.RemoveProductTag, Shared.CatalogAdminTools.CreateCategory,
        Shared.CatalogAdminTools.UpdateCategory, Shared.CatalogAdminTools.CreateAuthor,
        Shared.CatalogAdminTools.CreateProductTag, Shared.CatalogAdminTools.RenameProductTag,
        Shared.CatalogAdminTools.ListProductTags, Shared.CatalogAdminTools.CreateSpecificationAttribute,
        Shared.CatalogAdminTools.AddSpecificationAttributeOption, Shared.CatalogAdminTools.ListSpecificationAttributes,
        Shared.CatalogAdminTools.RepublishProducts,
        // 083: Excel katalog import — hepsi YALNIZ /mcp-admin.
        Shared.CatalogAdminTools.ImportCatalog, Shared.CatalogAdminTools.PublishImported,
        Shared.CatalogAdminTools.GetImportStatus,
    ];

    // /mcp-admin scope demeti (okuma + yazma). AddAuth + RFC 9728 PRM keşfi aynı kümeyi kullanır.
    public static readonly string[] Scopes =
    [
        AuthorizationScopes.AdminCatalogRead,
        AuthorizationScopes.AdminCatalogWrite,
    ];
}