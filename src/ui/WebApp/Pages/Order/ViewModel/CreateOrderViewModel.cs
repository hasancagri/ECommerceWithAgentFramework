#region

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using WebApp.Pages.Account.Dto;
using WebApp.Pages.Basket.ViewModel;

#endregion

namespace WebApp.Pages.Order.ViewModel;

// 023: Checkout'ta adres/kart ELLE girilmez; kullanici yalniz kayitli kayitlardan SECER.
public record CreateOrderViewModel
{
    // Kullanicinin secimi (kayitli kayit id'leri).
    public Guid? SelectedAddressId { get; set; }
    public Guid? SelectedCardId { get; set; }

    // Secilebilir kayitli veriler (Customer API'den; salt gosterim/secim, PAN/CVV yok).
    [ValidateNever] public List<AddressItemDto> SavedAddresses { get; set; } = [];
    [ValidateNever] public List<CardItemDto> SavedCards { get; set; } = [];

    // OnPost'ta id'den cozulen secili kayitlar (OrderService kullanir).
    [ValidateNever] public AddressItemDto? SelectedAddress { get; set; }
    [ValidateNever] public CardItemDto? SelectedCard { get; set; }

    [ValidateNever] public List<OrderItemViewModel> OrderItems { get; set; } = [];

    public decimal TotalPrice { get; set; }

    public static CreateOrderViewModel Empty => new();

    public void AddOrderItem(BasketItemViewModel basketItem)
    {
        OrderItems.Add(new OrderItemViewModel(basketItem.Id, basketItem.Name,
            basketItem.Price, basketItem.Quantity));
    }
}