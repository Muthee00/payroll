using PayMaster.Application.Common.Interfaces;

namespace PayMaster.Application.Reports;

/// <summary>Turns any <see cref="ReportResult"/> into Excel or PDF.</summary>
public sealed class ReportExporter(ISpreadsheetService spreadsheets, IReportPdfRenderer pdf, IApplicationDbContext db, IAuditService audit)
{
    public async Task<byte[]> ToExcelAsync(ReportResult report, CancellationToken ct = default)
    {
        var columns = report.Columns.Select((c, i) => new SpreadsheetColumn<object?[]>(c.Header, row => row[i], c.Kind switch
        {
            ReportColumnKind.Money => "#,##0.00",
            ReportColumnKind.Percent => "0.0%",
            ReportColumnKind.Number => "#,##0",
            _ => null
        })).ToList();

        var rows = report.Rows.ToList();
        if (report.HasTotals)
        {
            var totals = report.Totals;
            totals[0] ??= "TOTAL";
            rows.Add(totals);
        }

        await RecordAsync(report, "xlsx", ct);
        return spreadsheets.Export(report.Title, rows, columns, $"{report.Title} — {report.Subtitle}");
    }

    public async Task<byte[]> ToPdfAsync(ReportResult report, CancellationToken ct = default)
    {
        var company = db.CompanySettings.Select(c => c.CompanyName).FirstOrDefault() ?? "PayMaster";
        await RecordAsync(report, "pdf", ct);
        return pdf.Render(report, company);
    }

    private async Task RecordAsync(ReportResult report, string format, CancellationToken ct)
    {
        audit.Record("Report.Exported", "Report", report.Key, $"{report.Title} ({report.Subtitle}) as {format}, {report.Rows.Count} rows");
        await db.SaveChangesAsync(ct);
    }
}
