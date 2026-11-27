using System.Globalization;
using PayMaster.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PayMaster.Infrastructure.Services;

/// <summary>Landscape A4 table report.</summary>
public sealed class QuestPdfReportRenderer : IReportPdfRenderer
{
    static QuestPdfReportRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(ReportResult report, string companyName) => Document.Create(container => container.Page(page =>
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(24);
        page.DefaultTextStyle(t => t.FontSize(report.Columns.Count > 9 ? 7 : 8));

        page.Header().PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(report.Title).FontSize(15).Bold().FontColor("#0B1736");
                c.Item().Text(report.Subtitle).FontColor("#64748B");
            });
            row.ConstantItem(220).AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text(companyName).SemiBold();
                c.Item().AlignRight().Text($"Generated {DateTime.UtcNow.AddHours(3):dd MMM yyyy HH:mm} EAT").FontColor("#64748B");
            });
        });

        page.Content().Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    foreach (var c in report.Columns)
                        if (c.Kind == ReportColumnKind.Text) cols.RelativeColumn(1.4f); else cols.RelativeColumn();
                });
                table.Header(h =>
                {
                    foreach (var c in report.Columns)
                        Cell(h.Cell().Background("#0B1736"), c, c.Header, header: true);
                });
                var i = 0;
                foreach (var row in report.Rows)
                {
                    var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F5F7FB";
                    for (var c = 0; c < report.Columns.Count; c++)
                        Cell(table.Cell().Background(bg), report.Columns[c], Format(report.Columns[c], row[c]));
                }
                if (report.HasTotals)
                {
                    var totals = report.Totals;
                    for (var c = 0; c < report.Columns.Count; c++)
                        Cell(table.Cell().BorderTop(1).BorderColor("#0B1736"), report.Columns[c],
                            c == 0 && totals[c] is null ? "TOTAL" : Format(report.Columns[c], totals[c]), bold: true);
                }
            });
            if (report.Rows.Count == 0)
                column.Item().PaddingTop(20).AlignCenter().Text("No data for the selected filters.").FontColor("#64748B");
            foreach (var note in report.Notes)
                column.Item().PaddingTop(8).Text(note).FontSize(7).FontColor("#64748B");
        });

        page.Footer().AlignRight().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(7).FontColor("#64748B"));
            t.Span("Confidential · Page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        });
    })).GeneratePdf();

    private static void Cell(IContainer cell, ReportColumn column, string text, bool header = false, bool bold = false)
    {
        var c = cell.PaddingVertical(3).PaddingHorizontal(4);
        if (column.Kind != ReportColumnKind.Text) c = c.AlignRight();
        var t = c.Text(text);
        if (header) t.FontColor(Colors.White).SemiBold();
        else if (bold) t.Bold();
    }

    private static string Format(ReportColumn column, object? value) => ReportFormat.Cell(column, value);
}
