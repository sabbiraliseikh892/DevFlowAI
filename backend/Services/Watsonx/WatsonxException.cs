namespace DevFlowAI.API.Services.Watsonx;

/// <summary>
/// Thrown when the IBM watsonx HTTP client encounters an unrecoverable error
/// (network failure, bad credentials, unexpected response shape, etc.).
/// </summary>
public sealed class WatsonxException : Exception
{
    public WatsonxException(string message) : base(message) { }
    public WatsonxException(string message, Exception innerException)
        : base(message, innerException) { }
}
