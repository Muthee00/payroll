using Hangfire;
using Microsoft.Extensions.Logging;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Payroll;
using PayMaster.Application.Payslips;

namespace PayMaster.Infrastructure.BackgroundJobs;

/// <summary>Hangfire entry points. Each job runs in its own DI scope with the requesting user attributed.</summary>
public sealed class PayrollJobs(
    JobUserContext jobUser,
    IUserLookup userLookup,
    PayrollCalculationService calculator,
    PayslipService payslips,
    ILogger<PayrollJobs> logger)
{
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task CalculatePayrollAsync(int runId, int userId, CancellationToken ct)
    {
        jobUser.Set(userId, await userLookup.GetUserNameAsync(userId, ct));
        logger.LogInformation("Background payroll calculation started for run {RunId}", runId);
        await calculator.CalculateAsync(runId, userId, ct);
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task GeneratePayslipsAsync(int runId, int userId, bool email, CancellationToken ct)
    {
        jobUser.Set(userId, await userLookup.GetUserNameAsync(userId, ct));
        await payslips.ProcessRunAsync(runId, email, userId, ct);
    }
}

public sealed class HangfireBackgroundJobs(IBackgroundJobClient client) : IBackgroundJobs
{
    public void EnqueuePayrollCalculation(int runId, int userId) =>
        client.Enqueue<PayrollJobs>(j => j.CalculatePayrollAsync(runId, userId, CancellationToken.None));

    public void EnqueuePayslipGeneration(int runId, int userId, bool email) =>
        client.Enqueue<PayrollJobs>(j => j.GeneratePayslipsAsync(runId, userId, email, CancellationToken.None));
}

/// <summary>Resolves a user's sign-in name for audit attribution in background jobs.</summary>
public interface IUserLookup
{
    Task<string?> GetUserNameAsync(int userId, CancellationToken ct);
}
