namespace Storefront.Api.Tests;

// Dizin harf dilimi saf çekirdekleri: harf normalizasyonu ("A".."Z" / "#", büyük harfe) ve
// bellek-içi eşleşme (SQL ILIKE / regex ikizi). Üç slice aynı çekirdeği bilinçli tekrar eder;
// sözleşme tek testten doğrulanır (temsilci: GetPublishersByLetter).
public class LetterIndexTests
{
    [Theory]
    [InlineData("A", "A")]
    [InlineData("a", "A")]
    [InlineData(" z ", "Z")]
    [InlineData("#", "#")]
    public void NormalizeLetter_ValidInput_UppercasesAndTrims(string input, string expected)
    {
        GetPublishersByLetter.NormalizeLetter(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("AB")]
    [InlineData("5")]
    [InlineData("Ç")]
    [InlineData("%")]
    public void NormalizeLetter_InvalidInput_ReturnsNull(string? input)
    {
        GetPublishersByLetter.NormalizeLetter(input).ShouldBeNull();
    }

    [Theory]
    [InlineData("Can Yayınları", "C", true)]
    [InlineData("can yayınları", "C", true)]
    [InlineData("Can Yayınları", "D", false)]
    [InlineData("1001 Kitap", "#", true)]
    [InlineData("Çınar Yayınları", "#", true)] // A-Z dışı ilk harf "#" kovasına düşer
    [InlineData("Can Yayınları", "#", false)]
    [InlineData("", "A", false)]
    [InlineData(null, "A", false)]
    public void MatchesLetter_MirrorsSqlSemantics(string? name, string letter, bool expected)
    {
        GetPublishersByLetter.MatchesLetter(name, letter).ShouldBe(expected);
    }
}
