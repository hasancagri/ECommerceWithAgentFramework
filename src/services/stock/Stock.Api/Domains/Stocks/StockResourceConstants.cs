namespace Stock.Api.Domains.Stocks;

// Stock context'ine ozel hata kodu sabitleri (Result pattern: Code serbest metin degil, sabittir).
public static class StockResourceConstants
{
    public static readonly string STOCK_QUANTITY_CANNOT_BE_NEGATIVE = "STOCK_QUANTITY_CANNOT_BE_NEGATIVE";

    // 012-stock-reservation
    public static readonly string STOCK_INSUFFICIENT = "STOCK_INSUFFICIENT";
    public static readonly string STOCK_NO_ACTIVE_RESERVATION = "STOCK_NO_ACTIVE_RESERVATION";
    public static readonly string STOCK_RESERVE_QUANTITY_INVALID = "STOCK_RESERVE_QUANTITY_INVALID";
}