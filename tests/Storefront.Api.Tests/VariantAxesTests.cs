using Storefront.Api.Domains.StorefrontView;
using Storefront.Api.Domains.StorefrontView.Features.Queries;
using Shouldly;
using Xunit;

namespace Storefront.Api.Tests;

// 045 US2: varyant ekseni türetme — üyeler arasında FARKLILAŞAN spec attribute'ları eksen olur.
public class VariantAxesTests
{
    private static StorefrontView Member(params (string Attr, string Opt)[] specs)
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyCatalog("Üye", "Açıklama", 10m, Guid.NewGuid(), "Marka",
            Guid.NewGuid(), "Kategori", null, false,
            specs.Select(s => SpecPair.Create(s.Attr, s.Opt)).ToList(), "FAM");
        return view;
    }

    [Fact]
    public void DeriveAxes_SingleDifferingAttribute_OneAxis()
    {
        var members = new[]
        {
            Member(("Renk", "Kırmızı")),
            Member(("Renk", "Siyah")),
            Member(("Renk", "Beyaz")),
        };

        var axes = GetProductFamily.DeriveAxes(members);

        axes.Count.ShouldBe(1);
        axes[0].Attribute.ShouldBe("Renk");
        axes[0].Options.ShouldBe(new[] { "Beyaz", "Kırmızı", "Siyah" }); // distinct + sıralı
    }

    [Fact]
    public void DeriveAxes_TwoDifferingAttributes_TwoAxes()
    {
        var members = new[]
        {
            Member(("Renk", "Siyah"), ("Materyal", "Çelik")),
            Member(("Renk", "Beyaz"), ("Materyal", "Plastik")),
        };

        var axes = GetProductFamily.DeriveAxes(members);

        axes.Select(a => a.Attribute).ShouldBe(new[] { "Materyal", "Renk" }); // attribute adına göre sıralı
    }

    [Fact]
    public void DeriveAxes_NoDifference_Empty()
    {
        var members = new[]
        {
            Member(("Renk", "Siyah")),
            Member(("Renk", "Siyah")),
        };

        GetProductFamily.DeriveAxes(members).ShouldBeEmpty(); // ayrışma yok → seçici ad-listesine düşer
    }

    [Fact]
    public void DeriveAxes_MissingValueOnOneMember_IsStillAxis()
    {
        var members = new[]
        {
            Member(("Renk", "Siyah")),
            Member(),                    // Renk yok → "—" sayılır, ayrışma var
        };

        var axes = GetProductFamily.DeriveAxes(members);

        axes.Count.ShouldBe(1);
        axes[0].Options.ShouldBe(new[] { "Siyah" }); // yalnız mevcut değerler listelenir
    }

    [Fact]
    public void DeriveAxes_SharedAttributeNotDiffering_Excluded()
    {
        // Materyal her üyede aynı (eksen değil); Renk ayrışır (eksen).
        var members = new[]
        {
            Member(("Renk", "Siyah"), ("Materyal", "Çelik")),
            Member(("Renk", "Beyaz"), ("Materyal", "Çelik")),
        };

        var axes = GetProductFamily.DeriveAxes(members);

        axes.Count.ShouldBe(1);
        axes[0].Attribute.ShouldBe("Renk");
    }
}
