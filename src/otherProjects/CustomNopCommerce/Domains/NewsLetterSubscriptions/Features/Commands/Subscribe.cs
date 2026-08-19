namespace CustomNopCommerce.Domains.NewsLetterSubscriptions.Features.Commands;

/// <summary>Bültene abone olma write-slice'ı. Aynı e-posta zaten aktifse hata; pasifse tekrar aktifleşir;
/// yoksa yeni abonelik açılır (e-posta tekliği burada query ile korunur).</summary>
public static class Subscribe
{
    public record SubscribeCommand(string Email);

    public class SubscribeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class SubscribeCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubscribeResponse>> Handle(
            SubscribeCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Email))
                return FeatureObjectResultModel<SubscribeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = MessagingResourceConstants.SUBSCRIPTION_EMAIL_REQUIRED });
            if (!cmd.Email.Contains('@') || !cmd.Email.Contains('.'))
                return FeatureObjectResultModel<SubscribeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = MessagingResourceConstants.SUBSCRIPTION_EMAIL_INVALID });

            var existing = await session.Query<NewsLetterSubscription>()
                .Where(s => s.Email == cmd.Email && !s.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                if (existing.Active)
                    return FeatureObjectResultModel<SubscribeResponse>.Error(new MessageItem
                    { Property = nameof(cmd.Email), Code = MessagingResourceConstants.SUBSCRIPTION_ALREADY_EXISTS });

                existing.Reactivate();
                session.Update(existing);
                await session.SaveChangesAsync(ct);
                return FeatureObjectResultModel<SubscribeResponse>.Ok(new SubscribeResponse { Id = existing.Id });
            }

            var subscription = NewsLetterSubscription.Create(cmd.Email);
            session.Store(subscription);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<SubscribeResponse>.Ok(new SubscribeResponse { Id = subscription.Id });
        }
    }
}

public static class SubscribeCommandEndpoint
{
    public static RouteGroupBuilder SubscribeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/subscribe", async ([FromBody] Subscribe.SubscribeCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<Subscribe.SubscribeResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("Subscribe");
        return group;
    }
}
