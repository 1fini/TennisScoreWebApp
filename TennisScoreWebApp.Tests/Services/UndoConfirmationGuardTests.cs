using TennisScoreWebApp.Services;

namespace TennisScoreWebApp.Tests.Services;

public class UndoConfirmationGuardTests
{
    [Fact]
    public void RequestedConfirmationMatchesOnlyTheReviewedScore()
    {
        var guard = new UndoConfirmationGuard();

        guard.Request("score-before-confirmation");

        Assert.True(guard.IsPending);
        Assert.True(guard.Matches("score-before-confirmation"));
        Assert.False(guard.Matches("score-after-signalr-update"));
    }

    [Fact]
    public void ClearPreventsAStaleConfirmationFromExecuting()
    {
        var guard = new UndoConfirmationGuard();
        guard.Request("reviewed-score");

        guard.Clear();

        Assert.False(guard.IsPending);
        Assert.False(guard.Matches("reviewed-score"));
    }
}
