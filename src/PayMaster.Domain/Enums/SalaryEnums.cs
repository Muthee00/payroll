namespace PayMaster.Domain.Enums;

public enum ComponentType
{
    Earning = 1,
    Deduction = 2
}

public enum ComponentCategory
{
    // Earnings
    Basic = 1,
    Allowance = 2,
    Overtime = 3,
    Bonus = 4,
    Commission = 5,
    OtherEarning = 6,

    // Deductions
    Helb = 20,
    SalaryAdvance = 21,
    Loan = 22,
    Insurance = 23,
    Pension = 24,
    Absenteeism = 25,
    Sacco = 26,
    OtherDeduction = 27
}

/// <summary>How a component interacts with PAYE.</summary>
public enum TaxTreatment
{
    /// <summary>Earning included in taxable pay.</summary>
    Taxable = 1,

    /// <summary>Earning excluded from taxable pay (only where the law exempts it).</summary>
    NonTaxable = 2,

    /// <summary>Deduction taken from net pay with no tax effect (loans, HELB, advances).</summary>
    PostTax = 3,

    /// <summary>Deduction that reduces taxable income, subject to a configured cap (registered pension).</summary>
    AllowableDeduction = 4,

    /// <summary>Deduction whose premium qualifies for insurance relief.</summary>
    InsuranceRelief = 5,

    /// <summary>Deduction that reduces gross pay before tax (absenteeism / unpaid leave).</summary>
    ReducesGross = 6
}

public enum AmountType
{
    Fixed = 1,
    PercentageOfBasic = 2
}

public enum SalaryStructureStatus
{
    Draft = 1,
    Active = 2,
    Archived = 3
}

public enum SalaryRevisionStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Superseded = 4
}
