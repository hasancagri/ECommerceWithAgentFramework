namespace Procurement.Api.Options;

// 047: tedarikçi-başı feed ucu (relatif path). Base host service-discovery'den (dinamik-key istisnası);
// path buradan (code → "/v1/feeds/{code}"). Her tedarikçi ayrı uçtan okunur (heterojen topoloji).
public class SupplierFeedEndpointsOptions
{
    public Dictionary<string, string> Paths { get; set; } = new();
}
