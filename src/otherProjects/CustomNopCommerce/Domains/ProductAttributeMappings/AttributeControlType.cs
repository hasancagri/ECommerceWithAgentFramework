namespace CustomNopCommerce.Domains.ProductAttributeMappings;

/// <summary>
/// Bir ürün-attribute eşlemesinin müşteriye nasıl sunulacağı (UI kontrol tipi). nopCommerce paritesi.
/// Dropdown/Radio tekil seçim; Checkboxes çoklu; TextBox/Multiline serbest metin; FileUpload dosya;
/// ColorSquares/ImageSquares görsel seçim.
/// </summary>
public enum AttributeControlType
{
    DropdownList = 1,
    RadioList = 2,
    Checkboxes = 3,
    TextBox = 4,
    MultilineTextbox = 10,
    Datepicker = 20,
    FileUpload = 30,
    ColorSquares = 40,
    ImageSquares = 45,
    ReadonlyCheckboxes = 50,
}
