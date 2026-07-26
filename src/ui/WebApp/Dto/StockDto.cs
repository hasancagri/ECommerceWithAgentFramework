namespace WebApp.Dto;

// 012: Stock API artik rezervasyon-farkindalikli doner. Available = satin alinabilir adet
// (OnHand - aktif rezervasyon, >=0); UI "son N adet" ve Add-To-Basket kapisi bunu kullanir.
public record StockDto(Guid ProductId, int OnHand, int Reserved, int Available);