namespace PayMaster.Application.Common.Interfaces;

/// <summary>Queues long-running payroll work (Hangfire in Infrastructure).</summary>
public interface IBackgroundJobs
{
    void EnqueuePayrollCalculation(int runId, int userId);
    void EnqueuePayslipGeneration(int runId, int userId, bool email);
}

/// <summary>
/// Identity of the user who started a background job. When set, <see cref="ICurrentUser"/> reports this
/// user so audit entries and notifications from jobs are attributed correctly.
/// </summary>
public sealed class JobUserContext
{
    public int? UserId { get; private set; }
    public string? UserName { get; private set; }

    public void Set(int userId, string? userName)
    {
        UserId = userId;
        UserName = userName;
    }
}
