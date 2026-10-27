using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace AttendanceSystem.Tests.Unit;

public class RulesAndStoreTests
{
    private static readonly DateTime Start = new(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, AttendanceStatus.Present)]
    [InlineData(10, AttendanceStatus.Present)]   // exactly at the limit is still on time
    [InlineData(11, AttendanceStatus.Late)]
    public void LateDetection(int minutesAfterStart, AttendanceStatus expected) =>
        AttendanceRules.DetermineStatus(Start.AddMinutes(minutesAfterStart), Start, 10).Should().Be(expected);

    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(3, 4, 75)]
    [InlineData(1, 4, 25)]
    [InlineData(8, 8, 100)]
    public void PercentCalculation(int attended, int total, double expected) =>
        AttendanceRules.Percent(attended, total).Should().Be(expected);

    [Fact]
    public void ThresholdIsExclusive()
    {
        AttendanceRules.IsBelowThreshold(74.9, 75).Should().BeTrue();
        AttendanceRules.IsBelowThreshold(75, 75).Should().BeFalse();
    }

    [Fact]
    public async Task QrToken_LivesThirtySeconds_AndOldTokenSurvivesRotation()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryQrTokenStore(clock);
        var session = Guid.NewGuid();

        var first = await store.RotateAsync(session);
        (await store.IsValidAsync(session, first)).Should().BeTrue();

        clock.Advance(TimeSpan.FromSeconds(28));              // rotation tick
        var second = await store.RotateAsync(session);
        second.Should().NotBe(first);
        (await store.IsValidAsync(session, first)).Should().BeTrue("a scan that started just before rotation must still succeed");
        (await store.GetCurrentAsync(session)).Should().Be(second);

        clock.Advance(TimeSpan.FromSeconds(3));               // 31 s after the first token was issued
        (await store.IsValidAsync(session, first)).Should().BeFalse();
        (await store.IsValidAsync(session, second)).Should().BeTrue();

        await store.RemoveAsync(session);
        (await store.IsValidAsync(session, second)).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownToken_IsInvalid()
    {
        var store = new InMemoryQrTokenStore(TimeProvider.System);
        (await store.IsValidAsync(Guid.NewGuid(), "nope")).Should().BeFalse();
    }
}
