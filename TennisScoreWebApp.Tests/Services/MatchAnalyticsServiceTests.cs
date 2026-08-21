using TennisScoreWebApp.Infrastructure.ExternalServices.TennisScoreApi;
using TennisScoreWebApp.Services;

namespace TennisScoreWebApp.Tests.Services;

public class MatchAnalyticsServiceTests
{
    [Fact]
    public async Task LoadReturnsAnalyticsForInProgressMatch()
    {
        var analytics = CreateAnalytics(isCompleted: false, serviceContextAvailable: true);
        var service = new MatchAnalyticsService(new FakeMatchAnalyticsApi { Analytics = analytics });

        var result = await service.LoadAsync(analytics.MatchId);

        Assert.True(result.Succeeded);
        Assert.Same(analytics, result.Analytics);
        Assert.False(result.Analytics!.IsCompleted);
    }

    [Fact]
    public async Task LoadReturnsAnalyticsForCompletedMatch()
    {
        var analytics = CreateAnalytics(isCompleted: true, serviceContextAvailable: true);
        var service = new MatchAnalyticsService(new FakeMatchAnalyticsApi { Analytics = analytics });

        var result = await service.LoadAsync(analytics.MatchId);

        Assert.True(result.Succeeded);
        Assert.True(result.Analytics!.IsCompleted);
    }

    [Theory]
    [InlineData(404, MatchAnalyticsLoadOutcome.MatchNotFound, "This match could not be found.")]
    [InlineData(500, MatchAnalyticsLoadOutcome.UnexpectedError, "We couldn't load statistics for this match. Try again in a moment.")]
    public async Task LoadMapsApiFailuresToClearOutcomes(
        int statusCode,
        MatchAnalyticsLoadOutcome expectedOutcome,
        string expectedMessage)
    {
        var service = new MatchAnalyticsService(new FakeMatchAnalyticsApi
        {
            Exception = CreateApiException(statusCode)
        });

        var result = await service.LoadAsync(Guid.NewGuid());

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Analytics);
    }

    [Fact]
    public void PresentationContainsOnlyTheEightSupportedMetrics()
    {
        var metrics = MatchAnalyticsPresentation.CreateMetrics(
            CreateAnalytics(isCompleted: false, serviceContextAvailable: true));

        Assert.Equal(
            [
                "Total points won",
                "Aces",
                "Double faults",
                "Winners",
                "Unforced errors",
                "Forced errors",
                "Points served",
                "Points returned"
            ],
            metrics.Select(metric => metric.Label));
    }

    [Fact]
    public void PresentationDoesNotTreatMissingServiceContextAsZero()
    {
        var analytics = CreateAnalytics(isCompleted: false, serviceContextAvailable: false);
        analytics.Player1.PointsServed = 0;
        analytics.Player2.PointsReturned = 0;

        var metrics = MatchAnalyticsPresentation.CreateMetrics(analytics);
        var serviceMetrics = metrics.Where(metric => metric.Label is "Points served" or "Points returned");

        Assert.All(serviceMetrics, metric =>
        {
            Assert.False(metric.IsAvailable);
            Assert.Null(metric.Player1Value);
            Assert.Null(metric.Player2Value);
        });
    }

    [Fact]
    public void PresentationPreservesRealZeroWhenServiceContextIsAvailable()
    {
        var analytics = CreateAnalytics(isCompleted: false, serviceContextAvailable: true);
        analytics.Player1.PointsServed = 0;
        analytics.Player2.PointsServed = 0;

        var pointsServed = Assert.Single(
            MatchAnalyticsPresentation.CreateMetrics(analytics),
            metric => metric.Label == "Points served");

        Assert.True(pointsServed.IsAvailable);
        Assert.Equal(0, pointsServed.Player1Value);
        Assert.Equal(0, pointsServed.Player2Value);
    }

    [Fact]
    public void ErrorMetricsExplicitlyDescribePlayerAttribution()
    {
        var metrics = MatchAnalyticsPresentation.CreateMetrics(
            CreateAnalytics(isCompleted: true, serviceContextAvailable: true));

        var errorMetrics = metrics.Where(metric => metric.Label is "Double faults" or "Unforced errors" or "Forced errors");

        Assert.All(errorMetrics, metric => Assert.Equal("Committed by player", metric.Context));
    }

    private static MatchAnalyticsDto CreateAnalytics(bool isCompleted, bool serviceContextAvailable)
        => new()
        {
            MatchId = Guid.NewGuid(),
            IsCompleted = isCompleted,
            ServiceContextAvailable = serviceContextAvailable,
            Player1 = new PlayerMatchAnalyticsDto
            {
                PlayerId = Guid.NewGuid(),
                FirstName = "Player",
                LastName = "One",
                TotalPointsWon = 42,
                Aces = 5,
                DoubleFaults = 2,
                Winners = 11,
                UnforcedErrors = 8,
                ForcedErrors = 4,
                PointsServed = 50,
                PointsReturned = 45
            },
            Player2 = new PlayerMatchAnalyticsDto
            {
                PlayerId = Guid.NewGuid(),
                FirstName = "Player",
                LastName = "Two",
                TotalPointsWon = 38,
                Aces = 3,
                DoubleFaults = 4,
                Winners = 9,
                UnforcedErrors = 10,
                ForcedErrors = 6,
                PointsServed = 45,
                PointsReturned = 50
            }
        };

    private static ApiException CreateApiException(int statusCode)
        => new(
            "API error",
            statusCode,
            string.Empty,
            new Dictionary<string, IEnumerable<string>>(),
            null!);

    private sealed class FakeMatchAnalyticsApi : IMatchAnalyticsApi
    {
        public MatchAnalyticsDto Analytics { get; set; } = CreateAnalytics(false, true);
        public Exception? Exception { get; set; }

        public Task<MatchAnalyticsDto> GetStatsAsync(Guid matchId, CancellationToken cancellationToken)
            => Exception is null
                ? Task.FromResult(Analytics)
                : Task.FromException<MatchAnalyticsDto>(Exception);
    }
}
