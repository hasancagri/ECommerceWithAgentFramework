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
        var paymentRequest = new CreatePaymentRequest(
            viewModel.Payment.CardNumber,
            viewModel.Payment.CardHolderName,
            viewModel.Payment.ExpiryDate,
            viewModel.Payment.Cvv,
            viewModel.TotalPrice);

        var paymentResult = await paymentService.CreatePayment(paymentRequest);
        if (paymentResult.IsFail)
            return ServiceResult.Error(paymentResult.Fail!);

        // 2) Sonra siparis: donen paymentId ile.
        var address = new AddressDto(viewModel.Address.Province, viewModel.Address.District,
            viewModel.Address.Street, viewModel.Address.ZipCode, viewModel.Address.Line);

        var orderItems = viewModel.OrderItems
            .Select(x => new OrderItemDto(x.ProductId, x.ProductName, x.UnitPrice, x.Quantity))
            .ToList();

        var createOrderRequest = new CreateOrderRequest(
            viewModel.DiscountRate, address, paymentResult.Data, orderItems);

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
            var newOrderHistory =
                new OrderHistoryViewModel(orderResponse.Created.ToLongDateString(),
                    orderResponse.TotalPrice.ToString("C"));

            foreach (var orderItem in orderResponse.Items)
                newOrderHistory.AddItem(orderItem.ProductId, orderItem.ProductName, orderItem.UnitPrice, orderItem.Quantity);

            orderHistoryList.Add(newOrderHistory);
        }


        return ServiceResult<List<OrderHistoryViewModel>>.Success(orderHistoryList);
    }
}