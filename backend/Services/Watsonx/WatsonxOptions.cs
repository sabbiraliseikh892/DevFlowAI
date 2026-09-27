namespace DevFlowAI.API.Services.Watsonx;

/// <summary>
/// Strongly-typed configuration bound from the "Watsonx" config section.
/// Values are populated from environment variables or appsettings.json.
/// No secrets are hard-coded here — all fields are treated as placeholders.
/// </summary>
public sealed class WatsonxOptions
{
    public const string SectionName = "Watsonx";

    /// <summary>IBM Cloud region endpoint, e.g. "https://us-south.ml.cloud.ibm.com"</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>IBM Cloud IAM API key — set via IBM_WATSONX_API_KEY env var.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>watsonx.ai project ID — set via IBM_WATSONX_PROJECT_ID env var.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Foundation model ID, e.g. "ibm/granite-3-8b-instruct".
    /// Set via IBM_WATSONX_MODEL_ID env var.
    /// </summary>
    public string ModelId { get; set; } = "ibm/granite-3-8b-instruct";

    /// <summary>
    /// API version date for the watsonx text-generation endpoint.
    /// Defaults to the current stable version — override only when upgrading.
    /// </summary>
    public string ApiVersion { get; set; } = "2025-02-11";

    /// <summary>Maximum tokens the model should generate per response.</summary>
    public int MaxNewTokens { get; set; } = 1200;

    /// <summary>Returns true only when the minimum required fields are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Url) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ProjectId);
}
