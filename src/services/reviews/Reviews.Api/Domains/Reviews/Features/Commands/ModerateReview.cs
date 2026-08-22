using Reviews.Api.Infrastructure.Moderation;

namespace Reviews.Api.Domains.Reviews.Features.Commands;

// 044 FR-011/012: reviews.moderate kuyrugu tuketicisi. Yayin beklemez (fail-open) — bu handler
// arka planda kosar; hata ModerationException ile retry 10s/30s/60s → error queue.
public static class ModerateReview
{
    public const string QueueName = "reviews.moderate";

    // Metin tasinmaz — DB'den taze okunur (bayat kopya riski yok).
    public record ModerateReviewCommand(Guid ReviewId);

    [Transactional]
    public class ModerateReviewCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            ModerateReviewCommand cmd,
            IDocumentSession session,
            ModerationAgent agent,
            IMessageBus bus,
            CancellationToken ct)
        {
            var review = await session.LoadAsync<Review>(cmd.ReviewId, ct);
            if (review is null)
                return FeatureResultModel.Ok(); // silinmis/bilinmeyen id: sessiz no-op

            if (review.ModeratedAtUtc is not null)
                return FeatureResultModel.Ok(); // at-least-once: zaten denetlendi

            ModerationVerdict verdict;
            if (string.IsNullOrWhiteSpace(review.Text))
            {
                // Metinsiz yorum agent'a HIC gitmez: yalniz yildiz — ihlal edecek icerik yok.
                verdict = ModerationVerdict.Create(false, "none", "").Data!;
            }
            else
            {
                ModerationAgent.ModerationOutput output;
                try
                {
                    output = await agent.ModerateAsync(review.Text, review.Rating, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new ModerationException("Moderasyon LLM cagrisi basarisiz", ex);
                }

                var created = ModerationVerdict.Create(output.Violation, output.Category, output.Reason);
                if (!created.IsSuccess)
                    throw new ModerationException(
                        $"Sema disi moderasyon karari (category='{output.Category}')");
                verdict = created.Data!;
            }

            var wasVisible = review.Status == ReviewStatus.Visible;
            var applied = review.ApplyModeration(verdict, DateTimeOffset.UtcNow);
            if (!applied.IsSuccess)
                return FeatureResultModel.Error(applied.Messages);

            session.Store(review);

            // Ihlalde ozet duser: MUTLAK yeni ozet yayinlanir (Count=0 ⇒ tuketici temizler).
            if (wasVisible && review.Status == ReviewStatus.Hidden)
            {
                var remaining = await session.Query<Review>()
                    .Where(x => x.ProductId == review.ProductId
                                && x.Status == ReviewStatus.Visible
                                && x.Id != review.Id)
                    .Select(x => x.Rating)
                    .ToListAsync(ct);

                var count = remaining.Count;
                var average = count == 0 ? 0m : Math.Round((decimal)remaining.Average(), 2);
                await bus.PublishAsync(new IntegrationEvents.ReviewSummaryChanged(
                    review.ProductId, average, count));
            }

            return FeatureResultModel.Ok();
        }
    }
}
