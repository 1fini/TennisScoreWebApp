using TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi;

namespace TennisScoreWebApp.Services;

public interface IMatchUndoService
{
    Task<UndoMatchResult> UndoLastPointAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public enum UndoMatchOutcome
{
    Success,
    AlreadyInProgress,
    NoPointToUndo,
    MatchNotFound,
    RefreshFailed,
    UnexpectedError
}

public sealed record UndoMatchResult(
    UndoMatchOutcome Outcome,
    string Message,
    MatchDetailsDto? Match = null)
{
    public bool Succeeded => Outcome == UndoMatchOutcome.Success;
}

internal interface IUndoMatchApi
{
    Task UndoLastPointAsync(Guid matchId, CancellationToken cancellationToken);
    Task<MatchDetailsDto> GetMatchAsync(Guid matchId, CancellationToken cancellationToken);
}

internal sealed class TennisApiUndoMatchApi(ITennisApiClient client) : IUndoMatchApi
{
    public Task UndoLastPointAsync(Guid matchId, CancellationToken cancellationToken)
        => client.UndoLastPointAsync(matchId, cancellationToken);

    public Task<MatchDetailsDto> GetMatchAsync(Guid matchId, CancellationToken cancellationToken)
        => client.MatchesGETAsync(matchId, cancellationToken);
}

internal sealed class MatchUndoService(IUndoMatchApi api) : IMatchUndoService
{
    private int undoInProgress;

    public async Task<UndoMatchResult> UndoLastPointAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref undoInProgress, 1, 0) != 0)
        {
            return new(
                UndoMatchOutcome.AlreadyInProgress,
                "An undo is already in progress.");
        }

        try
        {
            try
            {
                await api.UndoLastPointAsync(matchId, cancellationToken);
            }
            catch (ApiException exception) when (exception.StatusCode == StatusCodes.Status409Conflict)
            {
                return new(
                    UndoMatchOutcome.NoPointToUndo,
                    "There is no recorded point to undo.");
            }
            catch (ApiException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
            {
                return new(
                    UndoMatchOutcome.MatchNotFound,
                    "This match could not be found.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return new(
                    UndoMatchOutcome.UnexpectedError,
                    "We couldn't undo the last point. The displayed score was not changed.");
            }

            try
            {
                var match = await api.GetMatchAsync(matchId, cancellationToken);
                return new(
                    UndoMatchOutcome.Success,
                    "Last point undone. The score is up to date.",
                    match);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return new(
                    UndoMatchOutcome.RefreshFailed,
                    "The point was undone, but the latest score could not be loaded. Wait for the live update or refresh the page.");
            }
        }
        finally
        {
            Volatile.Write(ref undoInProgress, 0);
        }
    }
}
