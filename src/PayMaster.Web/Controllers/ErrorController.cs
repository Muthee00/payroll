using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayMaster.Web.Controllers;

[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : AppController
{
    [Route("/error/{statusCode:int?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index(int? statusCode = null)
    {
        ViewData["StatusCode"] = statusCode ?? 500;
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Response.StatusCode = statusCode ?? 500;
        return View("Error");
    }
}
