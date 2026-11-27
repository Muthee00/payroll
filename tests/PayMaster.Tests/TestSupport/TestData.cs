using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Enums;

namespace PayMaster.Tests.TestSupport;

public static class TestData
{
    public static Department Department(string code = "ENG", string name = "Engineering") =>
        new() { Code = code, Name = name };

    public static Employee Employee(Department department, string number = "EMP-0001", string nationalId = "12345678") =>
        new()
        {
            EmployeeNumber = number,
            FirstName = "John",
            LastName = "Mwangi",
            Gender = Gender.Male,
            DateOfBirth = new DateOnly(1990, 5, 17),
            NationalId = nationalId,
            KraPin = $"A{nationalId}{number[^1]}Z"[..11],
            Department = department,
            EmploymentType = EmploymentType.Permanent,
            EmploymentDate = new DateOnly(2024, 1, 1),
            BankName = "Equity Bank",
            BankAccountNumber = "0123456789012"
        };
}
