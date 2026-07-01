using Microsoft.AspNetCore.SignalR.Client;
using TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi;

namespace TennisScoreWebApp.Services;

public class HubService(Uri uri) : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly Uri _hubUri = uri;
    private readonly HashSet<Guid> _matchGroups = [];
    private readonly HashSet<Guid> _tournamentGroups = [];
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public event Action<MatchDetailsDto>? OnMatchUpdated;

    public async Task StartAsync(Guid? matchId = null, Guid? tournamentId = null)
    {
        if (matchId.HasValue)
        {
            _matchGroups.Add(matchId.Value);
        }

        if (tournamentId.HasValue)
        {
            _tournamentGroups.Add(tournamentId.Value);
        }

        await EnsureConnectedAsync();

        if (matchId.HasValue)
        {
            await JoinMatchGroupAsync(matchId.Value);
        }

        if (tournamentId.HasValue)
        {
            await JoinTournamentGroupAsync(tournamentId.Value);
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            return;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                return;
            }

            _hubConnection ??= BuildConnection();
            await _hubConnection.StartAsync();
            await JoinKnownGroupsAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private HubConnection BuildConnection()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUri)
            .WithAutomaticReconnect()
            .Build();

        connection.On<MatchDetailsDto>("ReceiveMatchUpdate", match =>
        {
            OnMatchUpdated?.Invoke(match);
        });

        connection.Reconnected += async _ => await JoinKnownGroupsAsync();

        return connection;
    }

    private async Task JoinKnownGroupsAsync()
    {
        foreach (var matchId in _matchGroups)
        {
            await JoinMatchGroupAsync(matchId);
        }

        foreach (var tournamentId in _tournamentGroups)
        {
            await JoinTournamentGroupAsync(tournamentId);
        }
    }

    private Task JoinMatchGroupAsync(Guid matchId)
    {
        return _hubConnection?.InvokeAsync("JoinMatchGroup", matchId) ?? Task.CompletedTask;
    }

    private Task JoinTournamentGroupAsync(Guid tournamentId)
    {
        return _hubConnection?.InvokeAsync("JoinTournamentGroup", tournamentId) ?? Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        _matchGroups.Clear();
        _tournamentGroups.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _connectionLock.Dispose();
    }
}
