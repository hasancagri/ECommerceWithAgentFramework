namespace Basket.Api.Domains.Baskets.Features.Commands;

public static class AddBasketItem
{
    public record AddBasketItemCommand(
        Guid UserId,
        Guid ProductId,
        string ProductName,
        decimal Price,
        string? ImageUrl);

    public class AddBasketItemResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AddBasketItemCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddBasketItemResponse>> Handle(
            AddBasketItemCommand cmd,
            IDocumentSession session,
            StockReservationClientProxy reservation,
            IOptions<BasketReservationOptions> options,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            basket ??= Basket.Create(cmd.UserId);

            // 017 (FR-008, R6): sure dolmus sepette once tembel temizlik — satirlar duser, capa sifirlanir.
            // Stock'a Release CAGRILMAZ: rezervasyonlar ayni mutlak anda dolmustur, sweep supurur.
            var now = DateTimeOffset.UtcNow;
            basket.PurgeExpiredItems(now);

            // 017 (FR-002/003): capa adayi = mevcut capa ?? now + Duration. Rezervasyon capayla yaratilir;
            // basarida (yalniz capa yokken) kurulur — sonraki eklemeler capaya dokunmaz.
            var anchor = basket.ReservationExpiresAt ?? now.Add(options.Value.ReservationDuration);

            // 012: adet 1 artar; yeni toplam adet Stock'ta rezerve edilir (ayna). Yetersiz/erisilemez
            // ise sepete YAZILMAZ (fail-closed, FR-018/US1).
            var desiredQuantity = basket.GetItemQuantity(cmd.ProductId) + 1;

            // 021 (FR-005): sabit ust sinir otoriter — 5 ustune cikilamaz (UI/API/agent farketmez).
            if (desiredQuantity > Basket.MaxItemQuantity)
                return FeatureObjectResultModel<AddBasketItemResponse>.Error(
                    new MessageItem { Property = nameof(cmd.ProductId), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE });

            var reserve = await reservation.SetReservedQuantityAsync(cmd.ProductId, cmd.UserId, desiredQuantity, anchor, ct);
            if (!reserve.Success)
                return FeatureObjectResultModel<AddBasketItemResponse>.Error(
                    new MessageItem { Property = nameof(cmd.ProductId), Code = reserve.Code });

            // 021: kalan serbest stok saklanir (efektif max = min(5, adet+available)).
            basket.SetItem(cmd.ProductId, cmd.ProductName, cmd.ImageUrl, cmd.Price, desiredQuantity, reserve.Available);
            if (basket.ReservationExpiresAt is null)
                basket.StartReservation(anchor);

            session.Store(basket);
            return FeatureObjectResultModel<AddBasketItemResponse>.Ok(new AddBasketItemResponse { Id = basket.Id });
        }
    }
}

public static class AddBasketItemCommandEndpoint
{
    public static RouteGroupBuilder AddBasketItemGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/item", async ([FromBody] AddBasketItem.AddBasketItemCommand cmd, HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>>(cmd with { UserId = userId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddBasketItem")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}