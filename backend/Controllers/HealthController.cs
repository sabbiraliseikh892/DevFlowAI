using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevFlowAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    /// <summary>GET /api/health — lightweight liveness check.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var response = _healthService.GetStatus();
        return Ok(response);
    }
}
