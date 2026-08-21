namespace TennisScoreWebApp.Services;

internal sealed class UndoConfirmationGuard
{
    private string? requestedScoreSignature;

    public bool IsPending => requestedScoreSignature is not null;

    public void Request(string scoreSignature)
        => requestedScoreSignature = scoreSignature;

    public bool Matches(string currentScoreSignature)
        => requestedScoreSignature is not null
            && requestedScoreSignature == currentScoreSignature;

    public void Clear()
        => requestedScoreSignature = null;
}
