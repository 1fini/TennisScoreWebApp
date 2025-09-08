using Microsoft.AspNetCore.SignalR.Client;
using TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi;

namespace TennisScoreWebApp.Services;

public class HubService(Uri uri) : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly Uri _hubUri = uri;

    public event Action<MatchDetailsDto>? OnMatchUpdated;
    //public event Action<MatchDetailsDto>? OnPointReceived;

    public async Task StartAsync(Guid? matchId = null, Guid? tournamentId = null)
    {
        if (_hubConnection != null) return;

        // Add parameters to the URL if matchId or tournamentId are provided
        var hubUrl = _hubUri.ToString();
        if (matchId.HasValue || tournamentId.HasValue)
        {
            hubUrl += "?";
            if (matchId.HasValue) hubUrl += $"matchId={matchId}&";
            if (tournamentId.HasValue) hubUrl += $"tournamentId={tournamentId}&";
            hubUrl = hubUrl.TrimEnd('&');
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Event handlers for receiving updates
/*         _hubConnection.On<MatchDetailsDto>("ReceivePoint", match =>
        {
            OnPointReceived?.Invoke(match);
        }); */

        _hubConnection.On<MatchDetailsDto>("ReceiveMatchUpdate", match =>
        {
            OnMatchUpdated?.Invoke(match);
        });

        await _hubConnection.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
