using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminController : ApiControllerBase
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new AdminStatusResponse("Admin access granted."));
    }

    private sealed record AdminStatusResponse(string Message);
}
