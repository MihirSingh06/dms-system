using backend;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    [Authorize]
    [HttpGet("secure")]
    public IActionResult SecureEndpoint()
    {
        return Ok("You are authenticated.");
    }

    [Authorize(Roles = "Finance")]
    [HttpGet("finance")]
    public IActionResult FinanceOnly()
    {
        return Ok("Finance role access granted.");
    }

    [Authorize(Roles = "Manager")]
    [HttpGet("manager")]
    public IActionResult ManagerOnly()
    {
        return Ok("Manager role access granted.");
    }
}