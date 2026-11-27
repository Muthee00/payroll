using System.Globalization;
using PayMaster.Application.Payroll;
using PayMaster.Application.Payslips;
using PayMaster.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PayMaster.Infrastructure.Services;

/// <summary>A4 payslip PDF built with QuestPDF from the payroll snapshot.</summary>
public sealed class QuestPdfPayslipRenderer : IPayslipRenderer
{
    private const string Navy = "#0B1736";
    private const string Blue = "#1D5BF0";
    private const string Muted = "#64748B";
    private const string Border = "#E2E8F0";
    private const string Soft = "#F5F7FB";
    private static readonly CultureInfo Kenya = CultureInfo.GetCultureInfo("en-KE");

    static QuestPdfPayslipRenderer()
    {
        // Community licence: free for organisations below the QuestPDF revenue threshold. See docs/DEPLOYMENT.md.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(PayslipDocument document) => Build(document).GeneratePdf();

    public byte[] RenderMany(IReadOnlyList<PayslipDocument> documents) =>
        Document.Merge(documents.Select(d => (IDocument)Build(d)).ToArray()).UseOriginalPageNumbers().GeneratePdf();

    private static Document Build(PayslipDocument d) => Document.Create(container => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(t => t.FontSize(9).FontColor("#0F172A"));

        if (d.IsReversed)
            page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                .Text("REVERSED").FontSize(90).Bold().FontColor("#FECACA");

        page.Header().Element(c => Header(c, d));
        page.Content().PaddingVertical(14).Element(c => Content(c, d));
        page.Footer().Element(c => Footer(c, d));
    })).WithMetadata(new DocumentMetadata
    {
        Title = $"Payslip {d.PayslipNumber}",
        Author = d.Company.Name,
        Subject = $"{d.Line.EmployeeName} — {d.PeriodName}",
        Creator = "PayMaster"
    });

    private static void Header(IContainer container, PayslipDocument d)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Row(left =>
                {
                    if (d.Company.Logo is { Length: > 0 } logo)
                        left.ConstantItem(56).Height(56).PaddingRight(10).Image(logo).FitArea();
                    left.RelativeItem().Column(c =>
                    {
                        c.Item().Text(d.Company.Name).FontSize(16).Bold().FontColor(Navy);
                        if (!string.IsNullOrWhiteSpace(d.Company.Address)) c.Item().Text(d.Company.Address).FontColor(Muted);
                        if (!string.IsNullOrWhiteSpace(d.Company.PostalAddress)) c.Item().Text(d.Company.PostalAddress).FontColor(Muted);
                        var contact = string.Join("  ·  ", new[] { d.Company.Phone, d.Company.Email, d.Company.Website }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (contact.Length > 0) c.Item().Text(contact).FontColor(Muted);
                        if (!string.IsNullOrWhiteSpace(d.Company.KraPin)) c.Item().Text($"KRA PIN: {d.Company.KraPin}").FontColor(Muted);
                    });
                });

                row.ConstantItem(190).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("PAYSLIP").FontSize(20).Bold().FontColor(Blue);
                    c.Item().AlignRight().Text(d.PeriodName).FontSize(11).SemiBold();
                    c.Item().AlignRight().Text($"No. {d.PayslipNumber}").FontColor(Muted);
                    c.Item().AlignRight().Text($"Paid on {d.PaymentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}").FontColor(Muted);
                });
            });
            column.Item().PaddingTop(12).LineHorizontal(1.5f).LineColor(Navy);
        });
    }

    private static void Content(IContainer container, PayslipDocument d)
    {
        var l = d.Line;
        container.Column(column =>
        {
            column.Spacing(14);

            column.Item().Background(Soft).Padding(12).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(1.4f); c.RelativeColumn(); c.RelativeColumn(1.4f); });
                Pair(table, "Employee", l.EmployeeName, "Employee No.", l.EmployeeNumber);
                Pair(table, "Department", l.DepartmentName, "Job title", l.JobTitle ?? "—");
                Pair(table, "KRA PIN", l.KraPin ?? "—", "NSSF No.", l.NssfNumber ?? "—");
                Pair(table, "SHA No.", l.ShaNumber ?? "—", "Payment", PaymentText(l));
                Pair(table, "Pay period", $"{d.PeriodStart:dd MMM} – {d.PeriodEnd:dd MMM yyyy}", "Days paid",
                    l.DaysPayable == l.DaysInPeriod ? "Full month" : $"{l.DaysPayable} of {l.DaysInPeriod}");
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => AmountTable(c, "Earnings", l.Earnings, l.GrossEarnings, "Gross pay"));
                row.ConstantItem(16);
                row.RelativeItem().Element(c => AmountTable(c, "Deductions", l.Deductions, l.TotalDeductions, "Total deductions"));
            });

            column.Item().Background(Navy).Padding(12).Row(row =>
            {
                Summary(row.RelativeItem(), "Gross pay", l.GrossEarnings, "#C7D2E8");
                Summary(row.RelativeItem(), "Total deductions", l.TotalDeductions, "#C7D2E8");
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("NET PAY").FontSize(9).FontColor("#93C5FD").SemiBold();
                    c.Item().AlignRight().Text(Money(l.NetPay)).FontSize(17).Bold().FontColor(Colors.White);
                });
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => KeyValues(c, "Tax computation",
                [
                    ("Taxable earnings", l.TaxableEarnings),
                    ("Less allowable deductions (NSSF, SHIF, Housing Levy, pension)", -l.AllowableDeductions),
                    ("Taxable income", l.TaxableIncome),
                    ("Tax before reliefs", l.TaxBeforeReliefs),
                    ("Personal relief", -l.PersonalRelief),
                    ("Insurance relief", -l.InsuranceRelief),
                    ("PAYE", l.Paye)
                ]));
                row.ConstantItem(16);
                row.RelativeItem().Element(c => KeyValues(c, "Employer contributions (not deducted)",
                    l.EmployerContributions.Select(e => (e.Name, e.Amount))
                        .Append(("Total employer contributions", l.TotalEmployerContributions)).ToList()));
            });

            if (d.IsReversed)
                column.Item().Border(1).BorderColor("#FCA5A5").Background("#FEF2F2").Padding(8)
                    .Text("This payroll was reversed after issue. Refer to the correction payslip for the amounts payable.").FontColor("#B91C1C");
        });
    }

    private static void Footer(IContainer container, PayslipDocument d)
    {
        container.Column(c =>
        {
            c.Item().LineHorizontal(0.5f).LineColor(Border);
            c.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text(d.Company.Footer ?? "This is a computer-generated payslip.").FontSize(7.5f).FontColor(Muted);
                row.ConstantItem(140).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                    t.Span("Confidential · Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        });
    }

    private static void Pair(TableDescriptor table, string label1, string value1, string label2, string value2)
    {
        table.Cell().PaddingVertical(2).Text(label1).FontColor(Muted);
        table.Cell().PaddingVertical(2).Text(value1).SemiBold();
        table.Cell().PaddingVertical(2).Text(label2).FontColor(Muted);
        table.Cell().PaddingVertical(2).Text(value2).SemiBold();
    }

    private static void AmountTable(IContainer container, string title, IReadOnlyList<LineAmount> lines, decimal total, string totalLabel)
    {
        container.Border(1).BorderColor(Border).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(90); });
            table.Header(h =>
            {
                h.Cell().Background(Soft).Padding(6).Text(title).Bold().FontColor(Navy);
                h.Cell().Background(Soft).Padding(6).AlignRight().Text("KES").Bold().FontColor(Navy);
            });
            foreach (var line in lines.Where(x => x.Amount != 0 || x.Detail == nameof(DeductionKind.Statutory)))
            {
                table.Cell().BorderBottom(0.5f).BorderColor(Border).PaddingHorizontal(6).PaddingVertical(4).Text(line.Name);
                table.Cell().BorderBottom(0.5f).BorderColor(Border).PaddingHorizontal(6).PaddingVertical(4).AlignRight().Text(Number(line.Amount));
            }
            table.Cell().PaddingHorizontal(6).PaddingVertical(5).Text(totalLabel).Bold();
            table.Cell().PaddingHorizontal(6).PaddingVertical(5).AlignRight().Text(Number(total)).Bold();
        });
    }

    private static void KeyValues(IContainer container, string title, IReadOnlyList<(string Label, decimal Amount)> rows)
    {
        container.Column(c =>
        {
            c.Item().PaddingBottom(4).Text(title).Bold().FontColor(Navy);
            foreach (var (label, amount) in rows)
                c.Item().BorderBottom(0.5f).BorderColor(Border).PaddingVertical(3).Row(r =>
                {
                    r.RelativeItem().Text(label).FontColor(Muted);
                    r.ConstantItem(90).AlignRight().Text(Number(amount));
                });
        });
    }

    private static void Summary(IContainer container, string label, decimal amount, string labelColor) =>
        container.Column(c =>
        {
            c.Item().Text(label.ToUpperInvariant()).FontSize(8).FontColor(labelColor);
            c.Item().Text(Money(amount)).FontSize(12).SemiBold().FontColor(Colors.White);
        });

    private static string PaymentText(PayrollLineDetails l) => l.PaymentMethod switch
    {
        PaymentMethod.BankTransfer => $"{l.BankName} {l.MaskedAccount}".Trim(),
        PaymentMethod.MobileMoney => $"Mobile money {l.MaskedAccount}",
        _ => l.PaymentMethod.ToString()
    };

    private static string Number(decimal amount) => amount.ToString("N2", Kenya);
    private static string Money(decimal amount) => $"KSh {amount.ToString("N2", Kenya)}";
}
