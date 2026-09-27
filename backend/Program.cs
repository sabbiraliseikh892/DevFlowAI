using DevFlowAI.API.Services;
using DevFlowAI.API.Services.GitHub;
using DevFlowAI.API.Services.Watsonx;

var builder = WebApplication.CreateBuilder(args);

// ── Bind environment variables into the "Watsonx" config section ─────────────
// This lets operators override appsettings.json values without modifying files.
builder.Configuration
    .AddEnvironmentVariables()   // already the default; explicit for clarity
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Watsonx:Url"]       = Environment.GetEnvironmentVariable("IBM_WATSONX_URL"),
        ["Watsonx:ApiKey"]    = Environment.GetEnvironmentVariable("IBM_WATSONX_API_KEY"),
        ["Watsonx:ProjectId"] = Environment.GetEnvironmentVariable("IBM_WATSONX_PROJECT_ID"),
        ["Watsonx:ModelId"]   = Environment.GetEnvironmentVariable("IBM_WATSONX_MODEL_ID"),
    });

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSingleton<IHealthService, HealthService>();

// ── GitHub repository inspection ─────────────────────────────────────────────
// Uses the public GitHub REST API v3 (no OAuth required for public repos).
// Optionally reads GITHUB_TOKEN from the environment for a higher rate limit.
// The token is NEVER sent to the AI model or frontend.
builder.Services.AddHttpClient<IGitHubService, GitHubService>();

// ── IBM watsonx configuration (always registered; used only when provider = Watsonx) ──
builder.Services.Configure<WatsonxOptions>(
    builder.Configuration.GetSection(WatsonxOptions.SectionName));

builder.Services.AddHttpClient<IWatsonxHttpClient, WatsonxHttpClient>();

// ── Analysis provider selection ───────────────────────────────────────────────
// Reads "AnalysisProvider" from config (appsettings / env var).
// Defaults to "Mock" when the key is absent or blank.
var analysisProvider = builder.Configuration["AnalysisProvider"] ?? "Mock";
var watsonxOptions   = builder.Configuration
    .GetSection(WatsonxOptions.SectionName)
    .Get<WatsonxOptions>() ?? new WatsonxOptions();

if (analysisProvider.Equals("Watsonx", StringComparison.OrdinalIgnoreCase)
    && watsonxOptions.IsConfigured)
{
    builder.Services.AddScoped<IAnalysisService, WatsonxAnalysisService>();

    using var logFactory = LoggerFactory.Create(b => b.AddConsole());
    logFactory.CreateLogger("Startup")
        .LogInformation(
            "Analysis provider: IBM watsonx.ai  (model: {Model})",
            watsonxOptions.ModelId);
}
else
{
    builder.Services.AddSingleton<IAnalysisService, MockAnalysisService>();

    using var logFactory = LoggerFactory.Create(b => b.AddConsole());
    var log = logFactory.CreateLogger("Startup");

    if (analysisProvider.Equals("Watsonx", StringComparison.OrdinalIgnoreCase)
        && !watsonxOptions.IsConfigured)
    {
        log.LogWarning(
            "Analysis provider was set to 'Watsonx' but IBM watsonx credentials " +
            "are incomplete (Watsonx:Url, Watsonx:ApiKey, Watsonx:ProjectId are required). " +
            "Falling back to MockAnalysisService.");
    }
    else
    {
        log.LogInformation("Analysis provider: Mock (offline mode).");
    }
}

// ── CORS — allow the Vite dev server ──────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("DevCors");
//app.UseHttpsRedirection();
app.MapControllers();

app.Run();
