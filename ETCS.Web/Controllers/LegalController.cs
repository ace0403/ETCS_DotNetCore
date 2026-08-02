using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Web.Controllers;

[AllowAnonymous]
public sealed class LegalController : Controller
{
    [HttpGet]
    [Route("terms")]
    public IActionResult Terms() => View();

    [HttpGet]
    [Route("privacy")]
    public IActionResult Privacy() => View();

    [HttpGet]
    [Route("cancellation")]
    public IActionResult Cancellation() => View();
}
