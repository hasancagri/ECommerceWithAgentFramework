using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Shared;
using Shared.Enums;

namespace WebApp.ViewModel;

public record CreateProductViewModel
{
    public static CreateProductViewModel Empty => new();

    [Display(Name = "Product Name")] public string Name { get; init; } = null!;

    [Display(Name = "Description")] public string Description { get; init; } = null!;

    [Display(Name = "Price")] public decimal Price { get; init; }

    [Display(Name = "SKU")] public string Sku { get; init; } = null!;

    [Display(Name = "Brand")] public BrandType Brand { get; init; }

    [Display(Name = "Image URL")] public string? ImageUrl { get; init; }

    [Display(Name = "Initial Stock")] public int InitialStock { get; init; }

    public SelectList BrandDropdownList { get; set; } = null!;

    public void SetBrandDropdownList()
    {
        var brands = Enum.GetValues<BrandType>()
            .Select(b => new { Id = (int)b, Name = b.ToString() })
            .ToList();
        BrandDropdownList = new SelectList(brands, "Id", "Name");
    }
}