using Microsoft.AspNetCore.Mvc;

namespace SentinelLog.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json", "application/problem+json")]
public abstract class BaseApiController : ControllerBase
{
}
