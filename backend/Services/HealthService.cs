using DevFlowAI.API.DTOs;

namespace DevFlowAI.API.Services;

public class HealthService : IHealthService
{
    public HealthResponseDto GetStatus() =>
        new("healthy", "DevFlow AI API is running", DateTime.UtcNow);
}
