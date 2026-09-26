using DevFlowAI.API.DTOs;

namespace DevFlowAI.API.Services;

public interface IHealthService
{
    HealthResponseDto GetStatus();
}
