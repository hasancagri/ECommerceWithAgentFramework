using System.Net;


namespace WebApp.Services;

public class OrderService(
    IOrderRefitService orderService,
    ICheckoutRefitService checkoutService,
    ILogger<OrderService> logger)
{
    // Sipariş geçmişi tarihleri UTC saklanır (Marten audit) → gösterim için yerel saate (Türkiye) çevrilir.
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    // 049: checkout artık ayrı Checkout.Orchestrator'a gider. Payment ön-yaratımı KALKTI — orchestrator
    // tek-faz mock ödemeyi (Charge, pivot) + stok commit + onay + sepet temizliği saga'yla yürütür.
    // WebApp yalnız seçili adres+kart+kalemleri POST eder, 202 alır; süreç arka planda ilerler (FR-027).
    public async Task<ServiceResult> CreateOrder(CreateOrderViewModel viewModel)
    {
        var card = viewModel.SelectedCard!;
        var selected = viewModel.SelectedAddress!;

        var request = new StartCheckoutRequest(
            Items: viewModel.OrderItems
                .Select(x => new CheckoutItemRequest(x.ProductId, x.Quantity, x.ProductName, x.UnitPrice))
                .ToList(),
            Address: new CheckoutAddressRequest(selected.Province, selected.District, selected.Street, selected.ZipCode, selected.Line),
            CardRef: card.Id.ToString(),
            Installments: 1);

        var response = await checkoutService.StartCheckout(request);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return ServiceResult.FailFromProblemDetails(response.Error);

            logger.LogProblemDetails(response.Error);
            return ServiceResult.Error("An error occurred while starting checkout");
        }

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<List<OrderHistoryViewModel>>> GetHistory()
    {
        var response = await orderService.GetOrders();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<List<OrderHistoryViewModel>>.Error(
                "An error occurred while getting the order history");
        }

        var orderHistoryList = new List<OrderHistoryViewModel>();


        foreach (var orderResponse in response.Content)
        {
            // 028: durum rozeti (1=Beklemede, 2=Onaylandı, 3=İptal).
            var (statusText, badgeClass) = orderResponse.Status switch
            {
                1 => ("Beklemede", "bg-warning text-dark"),
                2 => ("Onaylandı", "bg-success"),
                3 => ("İptal", "bg-danger"),
                _ => ("Bilinmiyor", "bg-secondary")
            };

            var localCreated = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(orderResponse.CreatedTime, DateTimeKind.Utc), TurkeyTimeZone);

            var newOrderHistory =
                new OrderHistoryViewModel(
                    $"{localCreated.ToLongDateString()} {localCreated.ToShortTimeString()}",
                    orderResponse.TotalPrice.ToString("C"),
                    statusText,
                    badgeClass,
                    MapCancelReason(orderResponse.CancelReason));

            foreach (var orderItem in orderResponse.Items)
                newOrderHistory.AddItem(orderItem.ProductId, orderItem.ProductName, orderItem.UnitPrice, orderItem.Quantity);

            orderHistoryList.Add(newOrderHistory);
        }


        return ServiceResult<List<OrderHistoryViewModel>>.Success(orderHistoryList);
    }

    // 028: iptal sebep kodunu kullanici metnine cevirir (bilinmeyen kod oldugu gibi gosterilir).
    private static string? MapCancelReason(string? code) => code switch
    {
        null or "" => null,
        "ORDER_TIMEOUT" => "Zaman aşımı — stok işlemi tamamlanamadı",
        "STOCK_INSUFFICIENT" => "Yetersiz stok",
        "STOCK_NO_ACTIVE_RESERVATION" => "Rezervasyon süresi dolmuş",
        "STOCK_COMMIT_UNAVAILABLE" => "Stok servisine ulaşılamadı",
        "ORDER_STOCK_STEP_FAILED" => "Stok işlemi başarısız",
        _ => code
    };
}