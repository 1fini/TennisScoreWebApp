using TennisScoreWebApp.Components;
using TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi;
using TennisScoreWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var scoreApiUrl = builder.Configuration["SCORE_API_URL"] ?? "https://localhost:7277/";
var scoreHubUrl = builder.Configuration["SCOREHUB_URL"] ?? "http://localhost:5227/scoreHub";
var enableHttpsRedirection =
    bool.TryParse(builder.Configuration["ENABLE_HTTPS_REDIRECTION"], out var parsedEnableHttpsRedirection)
    && parsedEnableHttpsRedirection;

// Injection du HttpClient pour TennisApiClient
builder.Services.AddHttpClient<ITennisApiClient, TennisApiClient>(client =>
{
    client.BaseAddress = new Uri(scoreApiUrl);
});

builder.Services.AddScoped(_ => new HubService(new Uri(scoreHubUrl)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    if (enableHttpsRedirection)
    {
        app.UseHsts();
    }
}

if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
