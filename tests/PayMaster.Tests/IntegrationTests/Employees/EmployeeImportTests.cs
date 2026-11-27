using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayMaster.Domain.Enums;
using Shouldly;

namespace PayMaster.Tests.IntegrationTests.Employees;

[Trait("Category", "MySql")]
[Collection(PayMaster.Tests.TestSupport.MySqlCollection.Name)]
public class EmployeeImportTests(EmployeeFixture f) : IClassFixture<EmployeeFixture>
{
    private static readonly string[] Headers =
        ["FirstName", "LastName", "Gender", "DateOfBirth", "NationalId", "KraPin", "DepartmentCode", "PositionCode",
         "EmploymentType", "EmploymentDate", "PaymentMethod", "BankName", "BankAccountNumber", "BasicSalary", "SalaryStructureCode"];

    [Fact]
    public async Task Valid_file_imports_every_row_with_pending_salaries()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        using var file = Workbook(
            ["Amina", "Hassan", "Female", "1994-02-11", "40000001", "A400000001K", "ENG", "SWE", "Permanent", "2026-11-01", "BankTransfer", "KCB Bank", "1100223344", "95000", "SS-SWE"],
            ["Otieno", "Odhiambo", "Male", "1989-07-30", "40000002", "", "fin", "acc", "Contract", "2026-11-01", "MobileMoney", "", "", "", ""]);

        // Row 2 is a contract without an end date and mobile money without a number: fix it first.
        var result = await f.ImportExport(ctx).ImportAsync(file);
        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldAllBe(e => e.Row == 3, string.Join(" | ", result.Errors.Select(e => $"{e.Row}: {e.Message}")));
        result.Errors.ShouldContain(e => e.Message.Contains("Contract end date"));
        result.Errors.ShouldContain(e => e.Message.Contains("mobile money"));
        (await ctx.Employees.CountAsync(e => e.NationalId.StartsWith("4000000"))).ShouldBe(0); // all-or-nothing

        using var fixedFile = Workbook(
            ["Amina", "Hassan", "Female", "1994-02-11", "40000001", "A400000001K", "ENG", "SWE", "Permanent", "2026-11-01", "BankTransfer", "KCB Bank", "1100223344", "95000", "SS-SWE"],
            ["Otieno", "Odhiambo", "Male", "1989-07-30", "40000002", "", "fin", "acc", "Permanent", "2026-11-01", "BankTransfer", "Equity Bank", "0099887766", "", ""]);
        var ok = await f.ImportExport(ctx).ImportAsync(fixedFile);

        ok.Errors.ShouldBeEmpty();
        ok.Imported.ShouldBe(2);
        await using var verify = f.Db.CreateContext();
        var amina = await verify.Employees.Include(e => e.SalaryHistory).SingleAsync(e => e.NationalId == "40000001");
        amina.SalaryHistory.ShouldHaveSingleItem().Status.ShouldBe(SalaryRevisionStatus.PendingApproval);
        (await verify.AuditLogs.AnyAsync(a => a.Action == "Employee.BulkImported")).ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_codes_bad_dates_and_in_file_duplicates_are_reported_per_row()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        using var file = Workbook(
            ["Ann", "One", "Female", "31-31-1990", "50000001", "", "XXX", "", "Permanent", "2026-11-01", "BankTransfer", "KCB", "123456789", "", ""],
            ["Ben", "Two", "Robot", "1990-01-01", "50000002", "BADPIN", "ENG", "", "Permanent", "2026-11-01", "BankTransfer", "KCB", "123456789", "abc", ""],
            ["Cat", "Three", "Female", "1990-01-01", "50000002", "", "ENG", "", "Permanent", "2026-11-01", "BankTransfer", "KCB", "123456789", "", ""]);

        var result = await f.ImportExport(ctx).ImportAsync(file);

        result.Imported.ShouldBe(0);
        result.Errors.ShouldContain(e => e.Row == 2 && e.Message.Contains("not a valid date"));
        result.Errors.ShouldContain(e => e.Row == 2 && e.Message.Contains("Department code 'XXX'"));
        result.Errors.ShouldContain(e => e.Row == 3 && e.Message.Contains("Gender 'Robot'"));
        result.Errors.ShouldContain(e => e.Row == 3 && e.Message.Contains("KRA PIN"));
        result.Errors.ShouldContain(e => e.Row == 3 && e.Message.Contains("BasicSalary must be a number"));
        result.Errors.ShouldContain(e => e.Row == 4 && e.Message.Contains("appears more than once"));
    }

    [Fact]
    public async Task Export_includes_bank_details_only_with_permission()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        await f.Employees(ctx).CreateAsync(f.NewInput(first: "Export", last: "Target"));

        var withBank = Headers(await f.ImportExport(ctx).ExportAsync(new() { Search = "Target" }));
        withBank.ShouldContain("Account No.");

        f.Db.User.Permissions.Remove(Application.Common.Security.Permissions.Employees.ViewBankDetails);
        var withoutBank = Headers(await f.ImportExport(ctx).ExportAsync(new() { Search = "Target" }));
        withoutBank.ShouldNotContain("Account No.");
        withoutBank.ShouldContain("KRA PIN");

        static List<string> Headers(byte[] bytes)
        {
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            return wb.Worksheet(1).Row(4).CellsUsed().Select(c => c.GetString()).ToList();
        }
    }

    private static MemoryStream Workbook(params string[][] rows)
    {
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Employees");
        for (var c = 0; c < Headers.Length; c++)
            sheet.Cell(1, c + 1).Value = Headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(r + 2, c + 1).SetValue(rows[r][c]);
        var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
