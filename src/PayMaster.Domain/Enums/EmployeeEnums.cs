namespace PayMaster.Domain.Enums;

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}

public enum EmploymentType
{
    Permanent = 1,
    Contract = 2,
    Probation = 3,
    Casual = 4,
    Intern = 5
}

public enum EmploymentStatus
{
    Active = 1,
    OnLeave = 2,
    Suspended = 3,
    Deactivated = 4,
    Terminated = 5
}

public enum PayFrequency
{
    Monthly = 1
}

public enum PaymentMethod
{
    BankTransfer = 1,
    MobileMoney = 2,
    Cheque = 3,
    Cash = 4
}

public enum EmploymentChangeType
{
    Hired = 1,
    Transfer = 2,
    Promotion = 3,
    Demotion = 4,
    StatusChange = 5,
    ContractRenewal = 6,
    Termination = 7,
    Reactivation = 8
}

public enum EmployeeDocumentType
{
    NationalId = 1,
    KraPinCertificate = 2,
    EmploymentContract = 3,
    AcademicCertificate = 4,
    Cv = 5,
    DisabilityCertificate = 6,
    Other = 99
}
