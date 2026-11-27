namespace PayMaster.Application.Common.Interfaces;

/// <summary>Column definition for tabular exports. <see cref="Format"/> is an Excel number format (e.g. "#,##0.00").</summary>
public record SpreadsheetColumn<T>(string Header, Func<T, object?> Value, string? Format = null, double? Width = null);

/// <summary>Excel read/write (ClosedXML in Infrastructure).</summary>
public interface ISpreadsheetService
{
    byte[] Export<T>(string sheetName, IEnumerable<T> rows, IReadOnlyList<SpreadsheetColumn<T>> columns, string? title = null);

    /// <summary>Reads the first worksheet: header row → column names; each further non-empty row → values keyed by header.</summary>
    IReadOnlyList<IReadOnlyDictionary<string, string>> ReadRows(Stream workbook);

    /// <summary>Creates an import template with headers, notes and an example row.</summary>
    byte[] CreateTemplate(string sheetName, IReadOnlyList<(string Header, string Note, string Example)> columns);
}
