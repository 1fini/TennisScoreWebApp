using TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi;
using TennisScoreWebApp.Services;

namespace TennisScoreWebApp.Tests.Services;

public class MatchUndoServiceTests
{
    [Fact]
    public async Task UndoDuringGameReturnsRefreshedScore()
    {
        var match = CreateMatch(player1Score: "15", player2Score: "0");
        var api = new FakeUndoMatchApi { MatchToReturn = match };
        var service = new MatchUndoService(api);

        var result = await service.UndoLastPointAsync(match.Id);

        Assert.True(result.Succeeded);
        Assert.Same(match, result.Match);
        Assert.Equal("15", result.Match!.Player1.CurrentScore);
        Assert.Equal(1, api.UndoCalls);
        Assert.Equal(1, api.GetMatchCalls);
    }

    [Fact]
    public async Task UndoAfterGameWinningPointReturnsPreviousGameState()
    {
        var match = CreateMatch(player1Score: "0", player2Score: "0");
        match.Sets =
        [
            new SetScoreDto { Player1Games = 0, Player2Games = 0 }
        ];
        var api = new FakeUndoMatchApi { MatchToReturn = match };
        var service = new MatchUndoService(api);

        var result = await service.UndoLastPointAsync(match.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(0, Assert.Single(result.Match!.Sets).Player1Games);
        Assert.False(result.Match.IsCompleted);
    }

    [Fact]
    public async Task UndoMatchWinningPointReturnsReopenedMatch()
    {
        var reopenedMatch = CreateMatch(player1Score: "40", player2Score: "30");
        reopenedMatch.IsCompleted = false;
        reopenedMatch.EndTime = null;
        reopenedMatch.WinnerFirstName = null!;
        reopenedMatch.WinnerLastName = null!;
        var api = new FakeUndoMatchApi { MatchToReturn = reopenedMatch };
        var service = new MatchUndoService(api);

        var result = await service.UndoLastPointAsync(reopenedMatch.Id);

        Assert.True(result.Succeeded);
        Assert.False(result.Match!.IsCompleted);
        Assert.Null(result.Match.EndTime);
        Assert.True(string.IsNullOrWhiteSpace(result.Match.WinnerFirstName));
        Assert.Equal("40", result.Match.Player1.CurrentScore);
    }

    [Fact]
    public async Task RepeatedUndoWhileRequestIsInFlightDoesNotCallApiTwice()
    {
        var match = CreateMatch();
        var releaseUndo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var undoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeUndoMatchApi
        {
            MatchToReturn = match,
            UndoAction = async () =>
            {
                undoStarted.SetResult();
                await releaseUndo.Task;
            }
        };
        var service = new MatchUndoService(api);

        var firstUndo = service.UndoLastPointAsync(match.Id);
        await undoStarted.Task;
        var repeatedUndo = await service.UndoLastPointAsync(match.Id);
        releaseUndo.SetResult();
        var firstResult = await firstUndo;

        Assert.True(firstResult.Succeeded);
        Assert.Equal(UndoMatchOutcome.AlreadyInProgress, repeatedUndo.Outcome);
        Assert.Equal(1, api.UndoCalls);
        Assert.Equal(1, api.GetMatchCalls);
    }

    [Theory]
    [InlineData(409, UndoMatchOutcome.NoPointToUndo, "There is no recorded point to undo.")]
    [InlineData(404, UndoMatchOutcome.MatchNotFound, "This match could not be found.")]
    public async Task KnownApiFailuresReturnClearFeedback(
        int statusCode,
        UndoMatchOutcome expectedOutcome,
        string expectedMessage)
    {
        var api = new FakeUndoMatchApi
        {
            UndoException = CreateApiException(statusCode)
        };
        var service = new MatchUndoService(api);

        var result = await service.UndoLastPointAsync(Guid.NewGuid());

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Match);
        Assert.Equal(0, api.GetMatchCalls);
    }

    [Fact]
    public async Task RefreshFailureExplainsThatUndoMayAlreadyBeApplied()
    {
        var api = new FakeUndoMatchApi
        {
            GetMatchException = new HttpRequestException("offline")
        };
        var service = new MatchUndoService(api);

        var result = await service.UndoLastPointAsync(Guid.NewGuid());

        Assert.Equal(UndoMatchOutcome.RefreshFailed, result.Outcome);
        Assert.Contains("point was undone", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Match);
        Assert.True(result.RequiresScoreReconciliation);
    }

    [Fact]
    public async Task FailedUndoReleasesInFlightGuardForRetry()
    {
        var api = new FakeUndoMatchApi
        {
            UndoException = new HttpRequestException("offline")
        };
        var service = new MatchUndoService(api);

        var failedResult = await service.UndoLastPointAsync(Guid.NewGuid());
        api.UndoException = null;
        var retryResult = await service.UndoLastPointAsync(Guid.NewGuid());

        Assert.Equal(UndoMatchOutcome.UnexpectedError, failedResult.Outcome);
        Assert.True(retryResult.Succeeded);
        Assert.Equal(2, api.UndoCalls);
        Assert.Equal(1, api.GetMatchCalls);
    }

    [Fact]
    public async Task CancelledUndoReleasesInFlightGuardForRetry()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var api = new FakeUndoMatchApi
        {
            UndoException = new OperationCanceledException(cancellation.Token)
        };
        var service = new MatchUndoService(api);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.UndoLastPointAsync(Guid.NewGuid(), cancellation.Token));
        api.UndoException = null;
        var retryResult = await service.UndoLastPointAsync(Guid.NewGuid());

        Assert.True(retryResult.Succeeded);
        Assert.Equal(2, api.UndoCalls);
    }

    private static ApiException CreateApiException(int statusCode)
        => new(
            "API error",
            statusCode,
            string.Empty,
            new Dictionary<string, IEnumerable<string>>(),
            null!);

    private static MatchDetailsDto CreateMatch(
        string player1Score = "0",
        string player2Score = "0")
        => new()
        {
            Id = Guid.NewGuid(),
            Player1 = new PlayerDto
            {
                Id = Guid.NewGuid(),
                FirstName = "Player",
                LastName = "One",
                CurrentScore = player1Score
            },
            Player2 = new PlayerDto
            {
                Id = Guid.NewGuid(),
                FirstName = "Player",
                LastName = "Two",
                CurrentScore = player2Score
            },
            Sets = [],
            StartTime = DateTimeOffset.UtcNow
        };

    private sealed class FakeUndoMatchApi : IUndoMatchApi
    {
        public MatchDetailsDto MatchToReturn { get; set; } = CreateMatch();
        public Exception? UndoException { get; set; }
        public Exception? GetMatchException { get; set; }
        public Func<Task>? UndoAction { get; set; }
        public int UndoCalls { get; private set; }
        public int GetMatchCalls { get; private set; }

        public async Task UndoLastPointAsync(Guid matchId, CancellationToken cancellationToken)
        {
            UndoCalls++;
            if (UndoException is not null)
            {
                throw UndoException;
            }

            if (UndoAction is not null)
            {
                await UndoAction();
            }
        }

        public Task<MatchDetailsDto> GetMatchAsync(Guid matchId, CancellationToken cancellationToken)
        {
            GetMatchCalls++;
            return GetMatchException is not null
                ? Task.FromException<MatchDetailsDto>(GetMatchException)
                : Task.FromResult(MatchToReturn);
        }
    }
}
