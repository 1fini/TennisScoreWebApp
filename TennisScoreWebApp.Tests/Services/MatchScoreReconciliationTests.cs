using TennisScoreWebApp.Services;

namespace TennisScoreWebApp.Tests.Services;

public class MatchScoreReconciliationTests
{
    [Fact]
    public void RefreshFailureBlocksFurtherScoringWhileDisplayedScoreIsUnchanged()
    {
        var result = new UndoMatchResult(
            UndoMatchOutcome.RefreshFailed,
            "Undo applied but refresh failed.");

        Assert.True(MatchScoreReconciliation.IsPending(result, liveScoreChangedDuringRequest: false));
    }

    [Fact]
    public void LiveUpdateDuringUndoAlreadySatisfiesRefreshFailureReconciliation()
    {
        var result = new UndoMatchResult(
            UndoMatchOutcome.RefreshFailed,
            "Undo applied but refresh failed.");

        Assert.False(MatchScoreReconciliation.IsPending(result, liveScoreChangedDuringRequest: true));
    }

    [Theory]
    [InlineData(UndoMatchOutcome.Success)]
    [InlineData(UndoMatchOutcome.NoPointToUndo)]
    [InlineData(UndoMatchOutcome.UnexpectedError)]
    public void OtherOutcomesNeverLeaveReconciliationPending(UndoMatchOutcome outcome)
    {
        var result = new UndoMatchResult(outcome, "Finished.");

        Assert.False(MatchScoreReconciliation.IsPending(result, liveScoreChangedDuringRequest: false));
    }
}
