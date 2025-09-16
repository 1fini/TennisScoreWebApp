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

// Injection du HttpClient pour TennisApiClient
builder.Services.AddHttpClient<ITennisApiClient, TennisApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7277/"); // URL de ton API
});

builder.Services.AddSingleton(new HubService(new Uri(builder.Configuration["SCOREHUB_URL"]))); 

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
