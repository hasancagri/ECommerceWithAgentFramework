#region

using System.Collections.Immutable;

#endregion

namespace WebApp.Pages.Order.ViewModel;

public record OrderHistoryViewModel(string DateTime, string TotalPrice)
{
    private List<OrderItemViewModel> OrderItems { get; } = [];

    public ImmutableList<OrderItemViewModel> GetItems => OrderItems.ToImmutableList();


    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        OrderItems.Add(new OrderItemViewModel(productId, productName, unitPrice, quantity));
    }
}