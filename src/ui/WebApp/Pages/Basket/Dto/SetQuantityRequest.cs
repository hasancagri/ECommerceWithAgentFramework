namespace WebApp.Pages.Basket.Dto;

// 021: sepet satiri adedini mutlak degere getirir. Backend SetQuantityBody(int Quantity) karsiligi.
public record SetQuantityRequest(int Quantity);