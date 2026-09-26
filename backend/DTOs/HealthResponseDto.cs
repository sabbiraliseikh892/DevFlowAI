namespace DevFlowAI.API.DTOs;

public record HealthResponseDto(
    string Status,
    string Message,
    DateTime Timestamp
);
