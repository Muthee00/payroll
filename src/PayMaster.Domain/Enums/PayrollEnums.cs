namespace PayMaster.Domain.Enums;

public enum PayrollPeriodStatus
{
    Open = 1,
    Closed = 2
}

/// <summary>
/// Draft → Calculating → (Draft, calculated) → PendingApproval → Approved → Finalized → Paid.
/// Cancelled and Reversed are terminal and release the employee/period lock.
/// </summary>
public enum PayrollRunStatus
{
    Draft = 1,
    Calculating = 2,
    PendingApproval = 3,
    Approved = 4,
    Finalized = 5,
    Paid = 6,
    Cancelled = 7,
    Reversed = 8
}

public enum PayrollRunType
{
    Regular = 1,
    Correction = 2
}

public enum PayrollScope
{
    AllActive = 1,
    Department = 2,
    SelectedEmployees = 3
}

public enum PayrollLineStatus
{
    Pending = 1,
    Calculated = 2,
    Error = 3
}

public enum DeductionKind
{
    Statutory = 1,
    Voluntary = 2
}

public enum PayslipEmailStatus
{
    NotSent = 1,
    Queued = 2,
    Sent = 3,
    Failed = 4
}
