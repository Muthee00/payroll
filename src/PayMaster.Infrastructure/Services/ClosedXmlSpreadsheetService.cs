using ClosedXML.Excel;
using PayMaster.Application.Common.Interfaces;

namespace PayMaster.Infrastructure.Services;

public sealed class ClosedXmlSpreadsheetService : ISpreadsheetService
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#0B1736");

    public byte[] Export<T>(string sheetName, IEnumerable<T> rows, IReadOnlyList<SpreadsheetColumn<T>> columns, string? title = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeSheetName(sheetName));
        var headerRow = 1;

        if (!string.IsNullOrWhiteSpace(title))
        {
            sheet.Cell(1, 1).Value = title;
            sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
            sheet.Cell(2, 1).Value = $"Generated {DateTime.UtcNow.AddHours(3):dd MMM yyyy HH:mm} EAT";
            sheet.Cell(2, 1).Style.Font.SetFontColor(XLColor.Gray);
            headerRow = 4;
        }

        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(HeaderFill);
        }

        var r = headerRow + 1;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                SetValue(cell, columns[c].Value(row));
                if (columns[c].Format is { } format)
                    cell.Style.NumberFormat.Format = format;
            }
            r++;
        }

        if (r > headerRow + 1)
            sheet.Range(headerRow, 1, r - 1, columns.Count).SetAutoFilter();
        sheet.SheetView.FreezeRows(headerRow);
        for (var c = 0; c < columns.Count; c++)
        {
            if (columns[c].Width is double width)
                sheet.Column(c + 1).Width = width;
            else
                sheet.Column(c + 1).AdjustToContents(headerRow, Math.Min(r, headerRow + 500), 8, 60);
        }

        return Save(workbook);
    }

    public IReadOnlyList<IReadOnlyDictionary<string, string>> ReadRows(Stream workbookStream)
    {
        using var workbook = new XLWorkbook(workbookStream);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null)
            return [];

        var header = used.FirstRow();
        var headers = header.Cells().Select(c => c.GetString().Trim()).ToList();
        var result = new List<IReadOnlyDictionary<string, string>>();

        foreach (var row in used.RowsUsed().Skip(1))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                if (string.IsNullOrEmpty(headers[i]))
                    continue;
                var cell = row.Cell(i + 1);
                values[headers[i]] = cell.DataType switch
                {
                    XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd"),
                    XLDataType.Number => cell.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => cell.GetString().Trim()
                };
            }
            if (values.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                result.Add(values);
        }
        return result;
    }

    public byte[] CreateTemplate(string sheetName, IReadOnlyList<(string Header, string Note, string Example)> columns)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeSheetName(sheetName));
        for (var c = 0; c < columns.Count; c++)
        {
            var header = sheet.Cell(1, c + 1);
            header.Value = columns[c].Header;
            header.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(HeaderFill);
            header.CreateComment().AddText(columns[c].Note);
            var example = sheet.Cell(2, c + 1);
            example.SetValue(columns[c].Example);
            example.Style.Font.SetFontColor(XLColor.Gray).Font.SetItalic();
            sheet.Column(c + 1).Width = Math.Max(14, columns[c].Header.Length + 4);
        }
        sheet.SheetView.FreezeRows(1);

        var notes = workbook.Worksheets.Add("Instructions");
        notes.Cell(1, 1).Value = "How to use this template";
        notes.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        notes.Cell(2, 1).Value = "Replace the grey example row with your data (one employee per row). Hover over a header for its rules.";
        for (var c = 0; c < columns.Count; c++)
        {
            notes.Cell(c + 4, 1).Value = columns[c].Header;
            notes.Cell(c + 4, 1).Style.Font.SetBold();
            notes.Cell(c + 4, 2).Value = columns[c].Note;
        }
        notes.Column(1).Width = 26;
        notes.Column(2).Width = 90;
        return Save(workbook);
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = Blank.Value; break;
            case decimal d: cell.Value = d; break;
            case int i: cell.Value = i; break;
            case long l: cell.Value = l; break;
            case double db: cell.Value = db; break;
            case bool b: cell.Value = b ? "Yes" : "No"; break;
            case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "dd-mmm-yyyy"; break;
            case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd-mmm-yyyy hh:mm"; break;
            case Enum e: cell.Value = e.ToString(); break;
            default:
                // Prevent CSV/formula injection when the file is opened in a spreadsheet.
                var text = value.ToString() ?? string.Empty;
                cell.SetValue(text.Length > 0 && "=+-@".Contains(text[0]) ? "'" + text : text);
                break;
        }
    }

    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(c => !"[]*?/\\:".Contains(c)).ToArray());
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
