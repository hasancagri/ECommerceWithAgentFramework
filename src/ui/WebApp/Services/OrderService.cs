using System.Net;
using WebApp.Pages.Order.Dto;
using WebApp.Pages.Order.ViewModel;
using WebApp.Services.Refit;
using WebApp.Extensions;


namespace WebApp.Services;

public class OrderService(
    IOrderRefitService orderService,
    PaymentService paymentService,
    ILogger<OrderService> logger)
{
    public async Task<ServiceResult> CreateOrder(CreateOrderViewModel viewModel)
    {
        // 1) Once odeme: client dogrudan Payment'a (kullanici token'i).
        // 023: PAN/CVV yok; secili kayitli kartin gorunur alanlari doldurulur (Payment zaten yalniz
        // Amount kullanir). CardNumber = son 4 hane; CVV bos.
        var card = viewModel.SelectedCard!;
        var paymentRequest = new CreatePaymentRequest(
            card.Last4,
            card.Label ?? card.Brand,
            $"{card.ExpiryMonth:D2}/{card.ExpiryYear}",
            string.Empty,
            viewModel.TotalPrice);

        var paymentResult = await paymentService.CreatePayment(paymentRequest);
        if (paymentResult.IsFail)
            return ServiceResult.Error(paymentResult.Fail!);

        // 2) Sonra siparis: donen paymentId + secili kayitli adres ile.
        var selected = viewModel.SelectedAddress!;
        var address = new AddressDto(selected.Province, selected.District,
            selected.Street, selected.ZipCode, selected.Line);

        var orderItems = viewModel.OrderItems
            .Select(x => new OrderItemDto(x.ProductId, x.ProductName, x.UnitPrice, x.Quantity))
            .ToList();

        var createOrderRequest = new CreateOrderRequest(
            address, paymentResult.Data, orderItems);

        var response = await orderService.CreateOrder(createOrderRequest);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return ServiceResult.FailFromProblemDetails(response.Error);

            logger.LogProblemDetails(response.Error);
            return ServiceResult.Error("An error occurred while creating the order");
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

            var newOrderHistory =
                new OrderHistoryViewModel(orderResponse.CreatedTime.ToLongDateString(),
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