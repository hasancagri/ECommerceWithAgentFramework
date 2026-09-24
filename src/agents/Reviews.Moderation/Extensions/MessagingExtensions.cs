namespace Reviews.Moderation.Extensions;

// Reviews.Moderation mesajlaşma kurulumu: Wolverine + RabbitMQ (ReviewModerationRequested tüket →
// ReviewModerated yay) + retry/error-queue (fail-open) + handler keşfi. Program.cs orkestrasyon dışı.
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddModerationMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // Tuketici: kendi kuyrugunu deklare edilen exchange'e baglar (007 dersi) + dinler.
            rabbit.DeclareExchange(RabbitMqConstants.ReviewModerationRequested.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ReviewModerationRequested.Queues.Worker);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ReviewModerationRequested.Queues.Worker);

            // Yayinci: yalniz exchange deklare eder (binding tuketici Reviews'te).
            rabbit.DeclareExchange(RabbitMqConstants.ReviewModerated.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<IntegrationEvents.ReviewModerated>()
                .ToRabbitExchange(RabbitMqConstants.ReviewModerated.Exchange);

            // LLM hatasi: retry 10s/30s/60s → error queue. Fail-open: yorum Reviews'te Visible kalir.
            opts.OnException<ModerationException>()
                .RetryWithCooldown(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60))
                .Then.MoveToErrorQueue();

            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // TUZAK: *EventHandlers (çoğul) Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et.
            opts.Discovery.IncludeType(typeof(ReviewModerationEventHandlers));
        });

        return builder;
    }
}
