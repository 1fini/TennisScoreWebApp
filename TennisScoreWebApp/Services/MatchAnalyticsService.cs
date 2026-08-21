using TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi;

namespace TennisScoreWebApp.Services;

public interface IMatchAnalyticsService
{
    Task<MatchAnalyticsLoadResult> LoadAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public enum MatchAnalyticsLoadOutcome
{
    Success,
    MatchNotFound,
    UnexpectedError
}

public sealed record MatchAnalyticsLoadResult(
    MatchAnalyticsLoadOutcome Outcome,
    string Message,
    MatchAnalyticsDto? Analytics = null)
{
    public bool Succeeded => Outcome == MatchAnalyticsLoadOutcome.Success;
}

internal interface IMatchAnalyticsApi
{
    Task<MatchAnalyticsDto> GetStatsAsync(Guid matchId, CancellationToken cancellationToken);
}

internal sealed class TennisApiMatchAnalyticsApi(ITennisApiClient client) : IMatchAnalyticsApi
{
    public Task<MatchAnalyticsDto> GetStatsAsync(Guid matchId, CancellationToken cancellationToken)
        => client.StatsAsync(matchId, cancellationToken);
}

internal sealed class MatchAnalyticsService(IMatchAnalyticsApi api) : IMatchAnalyticsService
{
    public async Task<MatchAnalyticsLoadResult> LoadAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var analytics = await api.GetStatsAsync(matchId, cancellationToken);
            return new(
                MatchAnalyticsLoadOutcome.Success,
                "Match statistics loaded.",
                analytics);
        }
        catch (ApiException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            return new(
                MatchAnalyticsLoadOutcome.MatchNotFound,
                "This match could not be found.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(
                MatchAnalyticsLoadOutcome.UnexpectedError,
                "We couldn't load statistics for this match. Try again in a moment.");
        }
    }
}

internal sealed record MatchAnalyticsMetric(
    string Label,
    int? Player1Value,
    int? Player2Value,
    bool IsAvailable = true,
    string? Context = null);

internal static class MatchAnalyticsPresentation
{
    public static IReadOnlyList<MatchAnalyticsMetric> CreateMetrics(MatchAnalyticsDto analytics)
        =>
        [
            new("Total points won", analytics.Player1.TotalPointsWon, analytics.Player2.TotalPointsWon),
            new("Aces", analytics.Player1.Aces, analytics.Player2.Aces),
            new("Double faults", analytics.Player1.DoubleFaults, analytics.Player2.DoubleFaults, Context: "Committed by player"),
            new("Winners", analytics.Player1.Winners, analytics.Player2.Winners),
            new("Unforced errors", analytics.Player1.UnforcedErrors, analytics.Player2.UnforcedErrors, Context: "Committed by player"),
            new("Forced errors", analytics.Player1.ForcedErrors, analytics.Player2.ForcedErrors, Context: "Committed by player"),
            new(
                "Points served",
                analytics.ServiceContextAvailable ? analytics.Player1.PointsServed : null,
                analytics.ServiceContextAvailable ? analytics.Player2.PointsServed : null,
                analytics.ServiceContextAvailable),
            new(
                "Points returned",
                analytics.ServiceContextAvailable ? analytics.Player1.PointsReturned : null,
                analytics.ServiceContextAvailable ? analytics.Player2.PointsReturned : null,
                analytics.ServiceContextAvailable)
        ];

    public static string GetPlayerName(PlayerMatchAnalyticsDto player)
    {
        var name = $"{player.FirstName} {player.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "Player" : name;
    }
}
