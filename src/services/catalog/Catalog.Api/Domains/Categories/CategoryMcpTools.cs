namespace Catalog.Api.Domains.Categories;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
[McpServerToolType]
public static class ListCategoriesMcpTool
{
    [McpServerTool(Name = "list_categories")]
    [Description("Magazadaki kategorileri listeler (yalniz yayinda urunu olan kategoriler). Her kategori " +
                 "ad, ust kategori (parentCategory, varsa) ve urun sayisi (productCount) tasir. 'Hangi " +
                 "kategoriler var' tarzi kesif sorulari icin.")]
    public static Task<FeatureListResultModel<Features.Agents.ListCategoriesForAgent.CategoryItem>> ListCategoriesAsync(
        IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<Features.Agents.ListCategoriesForAgent.CategoryItem>>(
            new Features.Agents.ListCategoriesForAgent.ListCategoriesQuery(), ct);
}