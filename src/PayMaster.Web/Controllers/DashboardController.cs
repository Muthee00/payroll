using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Dashboard;

namespace PayMaster.Web.Controllers;

/// <summary>Role-aware landing page.</summary>
public class DashboardController(DashboardService dashboard) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await dashboard.GetAsync(ct));
}
