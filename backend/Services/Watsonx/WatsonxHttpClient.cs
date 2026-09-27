using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevFlowAI.API.Services.Watsonx;

/// <summary>
/// Low-level HTTP client for IBM watsonx.ai.
/// Responsibilities:
///   1. Exchange IBM Cloud API key → short-lived IAM bearer token (cached until near-expiry).
///   2. POST a prompt to the watsonx text-generation endpoint.
///   3. Return the raw generated text (caller is responsible for parsing).
///
/// This class knows nothing about analysis logic — it is a pure transport layer.
/// </summary>
public sealed class WatsonxHttpClient : IWatsonxHttpClient
{
    private const string IamTokenUrl =
        "https://iam.cloud.ibm.com/identity/token";
    private const string TextGenerationPath =
        "/ml/v1/text/generation";

    private readonly HttpClient _http;
    private readonly WatsonxOptions _opts;
    private readonly ILogger<WatsonxHttpClient> _logger;

    // IAM token cache — refreshed when within 60 seconds of expiry.
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // JSON options reused across serialisations.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public WatsonxHttpClient(
        HttpClient http,
        IOptions<WatsonxOptions> options,
        ILogger<WatsonxHttpClient> logger)
    {
        _http   = http;
        _opts   = options.Value;
        _logger = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends <paramref name="prompt"/> to the configured watsonx model and
    /// returns the raw generated text string.
    /// Throws <see cref="WatsonxException"/> on non-recoverable errors.
    /// </summary>
    public async Task<string> GenerateTextAsync(
        string prompt,
        CancellationToken ct = default)
    {
        var token = await GetOrRefreshTokenAsync(ct);

        var requestBody = new TextGenerationRequest(
            Input     : prompt,
            ModelId   : _opts.ModelId,
            ProjectId : _opts.ProjectId,
            Parameters: new GenerationParameters(_opts.MaxNewTokens)
        );

        var json    = JsonSerializer.Serialize(requestBody, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url     = $"{_opts.Url.TrimEnd('/')}{TextGenerationPath}?version={_opts.ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogDebug(
            "Sending text-generation request to watsonx. Model={Model} ProjectId={ProjectId}",
            _opts.ModelId, _opts.ProjectId);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new WatsonxException(
                "Network error communicating with IBM watsonx.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "watsonx returned HTTP {Status}: {Body}",
                (int)response.StatusCode, responseBody);
            throw new WatsonxException(
                $"IBM watsonx returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        var result = JsonSerializer.Deserialize<TextGenerationResponse>(responseBody, JsonOpts);

        var generatedText = result?.Results?.FirstOrDefault()?.GeneratedText
            ?? string.Empty;

        _logger.LogDebug(
            "watsonx response received. Generated {Length} characters.", generatedText.Length);

        return generatedText;
    }

    // ── IAM token management ──────────────────────────────────────────────────

    private async Task<string> GetOrRefreshTokenAsync(CancellationToken ct)
    {
        // Fast path — token still valid.
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // Re-check after acquiring lock (double-checked locking).
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
                return _cachedToken;

            _logger.LogDebug("Refreshing IBM IAM bearer token.");

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>(
                    "grant_type",
                    "urn:ibm:params:oauth:grant-type:apikey"),
                new KeyValuePair<string, string>("apikey", _opts.ApiKey),
            });

            HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await _http.PostAsync(IamTokenUrl, formData, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new WatsonxException(
                    "Network error retrieving IBM IAM token.", ex);
            }

            var tokenBody = await tokenResponse.Content.ReadAsStringAsync(ct);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                throw new WatsonxException(
                    $"IBM IAM token exchange failed with HTTP " +
                    $"{(int)tokenResponse.StatusCode}: {tokenResponse.ReasonPhrase}");
            }

            var iamResult = JsonSerializer.Deserialize<IamTokenResponse>(
                tokenBody, JsonOpts)
                ?? throw new WatsonxException("IBM IAM response could not be parsed.");

            _cachedToken   = iamResult.AccessToken;
            // Refresh 60 s before expiry to avoid using a near-expired token.
            _tokenExpiresAt = DateTimeOffset.UtcNow
                .AddSeconds(iamResult.ExpiresIn - 60);

            _logger.LogDebug(
                "IBM IAM token refreshed. Expires in {Seconds}s.",
                iamResult.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    // ── DTO records (internal to this file) ──────────────────────────────────

    private sealed record TextGenerationRequest(
        string Input,
        string ModelId,
        string ProjectId,
        GenerationParameters Parameters);

    private sealed record GenerationParameters(int MaxNewTokens);

    private sealed record TextGenerationResponse(
        IReadOnlyList<GeneratedResult>? Results);

    private sealed record GeneratedResult(
        string GeneratedText,
        string? StopReason);

    private sealed record IamTokenResponse(
        string AccessToken,
        int    ExpiresIn);
}
