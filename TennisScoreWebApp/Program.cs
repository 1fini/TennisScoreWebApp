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

// Injection du HttpClient pour TennisApiClient
builder.Services.AddHttpClient<ITennisApiClient, TennisApiClient>(client =>
{
    client.BaseAddress = new Uri(scoreApiUrl);
});

builder.Services.AddSingleton(new HubService(new Uri(scoreHubUrl))); 

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
