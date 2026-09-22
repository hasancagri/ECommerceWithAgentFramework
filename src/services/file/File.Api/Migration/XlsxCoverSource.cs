using ClosedXML.Excel;

namespace FileApi.Migration;

// catalog-import.xlsx'ten yalnız {isbn, imageUrl} kolonlarını okur (dosyada ~14 kolon var;
// gerisi bu feature'ın dışı). Başlık satırından kolon adıyla konum bulunur.
public sealed class XlsxCoverSource
{
    public IEnumerable<(string Isbn, string? ImageUrl)> Read(string xlsxPath)
    {
        using var wb = new XLWorkbook(xlsxPath);
        var ws = wb.Worksheet(1);

        var header = ws.Row(1);
        var isbnCol = FindColumn(header, "isbn");
        var imageCol = FindColumn(header, "imageUrl");

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var isbn = row.Cell(isbnCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(isbn)) continue;

            var imageUrl = row.Cell(imageCol).GetString().Trim();
            yield return (isbn, imageUrl);
        }
    }

    private static int FindColumn(IXLRow header, string name)
    {
        foreach (var cell in header.CellsUsed())
        {
            if (string.Equals(cell.GetString().Trim(), name, StringComparison.OrdinalIgnoreCase))
                return cell.Address.ColumnNumber;
        }
        throw new InvalidOperationException($"xlsx başlığında '{name}' kolonu yok.");
    }
}
