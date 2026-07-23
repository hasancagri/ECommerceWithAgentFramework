using Common.Utils.Constants;
using Discount.Api.Domains.Discounts.Features.Agent;

namespace Discount.Api.Tests;

// Agent yüzü idempotency (FR-022): indirimsiz üründe remove etkisiz başarıdır; diğer sonuçlar aynen geçer.
public class RemoveProductDiscountAgentTests
{
    [Fact]
    public void NotFound_BecomesOk_RemoveIsIdempotent()
    {
        var result = RemoveProductDiscount.RemoveProductDiscountCommandHandler
            .AsIdempotent(FeatureResultModel.NotFound());

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Ok_StaysOk()
    {
        var result = RemoveProductDiscount.RemoveProductDiscountCommandHandler
            .AsIdempotent(FeatureResultModel.Ok());

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void OtherErrors_AreNotSwallowed()
    {
        var error = FeatureResultModel.Error(new MessageItem { Code = "DISCOUNT_RATE_INVALID" });

        var result = RemoveProductDiscount.RemoveProductDiscountCommandHandler.AsIdempotent(error);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldNotBeNull();
        result.Messages[0].Code.ShouldBe("DISCOUNT_RATE_INVALID");
    }

    [Fact]
    public void OnlyRecordNotFoundCode_TriggersIdempotency()
    {
        var notFoundLike = FeatureResultModel.Error(
            new MessageItem { Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND });

        RemoveProductDiscount.RemoveProductDiscountCommandHandler
            .AsIdempotent(notFoundLike).IsSuccess.ShouldBeTrue();
    }
}