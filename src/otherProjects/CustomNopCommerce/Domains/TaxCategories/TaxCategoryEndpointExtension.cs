using CustomNopCommerce.Domains.TaxCategories.Features.Commands;
using CustomNopCommerce.Domains.TaxCategories.Features.Queries;

namespace CustomNopCommerce.Domains.TaxCategories;

/// <summary>Vergi kategorisi feature endpoint'lerini tek grup altında toplar.</summary>
public static class TaxCategoryEndpointExtension
{
    public static void AddTaxCategoryGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/tax-categories").WithTags("TaxCategories")
            .CreateTaxCategoryGroupItemEndpoint()
            .ListTaxCategoriesGroupItemEndpoint();
    }
}
