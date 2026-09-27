namespace DevFlowAI.API.Services.Watsonx;

/// <summary>
/// Contract for the IBM watsonx.ai text-generation client.
/// Extracted as an interface to allow test doubles without real HTTP calls.
/// </summary>
public interface IWatsonxHttpClient
{
    Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);
}
