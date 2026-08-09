
namespace Supplier.Gateway.Tests;

// 008 RunAsync sözleşmesi: kilit doluysa çekim yapılmadan sessiz dönülür (skipped);
// hata yutulmaz — job'ın failed olması ve Hangfire retry'ının işlemesi buna dayanır.
public class FeedPullRunTests
{
    private sealed class GateHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            await Release.Task;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    // Boş gövde → 0 kayıt → scope hiç kullanılmaz; null bağımlılıklar bu yüzden güvenlidir.
    private static FeedPullService Service(HttpMessageHandler handler) => new(
        httpClientFactory: new SingleClientFactory(handler),
        scopeFactory: null!,
        configuration: new ConfigurationBuilder().Build(),
        logger: NullLogger<FeedPullService>.Instance);

    [Fact]
    public async Task RunAsync_WhilePullInProgress_SkipsWithoutSecondFetch()
    {
        var handler = new GateHandler();
        var service = Service(handler);

        var first = service.RunAsync(CancellationToken.None);
        await handler.Entered.Task; // ilk çekim kilidin içinde, fetch'te bekliyor

        await service.RunAsync(CancellationToken.None); // kilit dolu → skipped, fetch'e inmez

        handler.Calls.ShouldBe(1);
        handler.Release.TrySetResult();
        await first;
    }

    [Fact]
    public async Task RunAsync_UnreachableFeed_ThrowsAndReleasesGate()
    {
        var service = Service(new FailingHandler());

        await Should.ThrowAsync<HttpRequestException>(() => service.RunAsync(CancellationToken.None));

        // Kilit bırakıldı: sonraki koşu yeniden fetch'e iner ve yine fırlatır (takılı kalmaz).
        await Should.ThrowAsync<HttpRequestException>(() => service.RunAsync(CancellationToken.None));
    }
}