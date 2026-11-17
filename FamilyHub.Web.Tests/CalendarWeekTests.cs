using FamilyHub.Web.Services;
using Xunit;

namespace FamilyHub.Web.Tests;

public sealed class CalendarWeekTests
{
    [Theory]
    [InlineData(2026, 10, 12, 2026, 10, 12)]
    [InlineData(2026, 10, 15, 2026, 10, 12)]
    [InlineData(2026, 10, 18, 2026, 10, 12)]
    [InlineData(2026, 10, 19, 2026, 10, 19)]
    public void GetStart_ReturnsMondayOfDate(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var weekStart = CalendarWeek.GetStart(new DateTime(year, month, day, 23, 45, 0));

        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), weekStart);
        Assert.Equal(DayOfWeek.Monday, weekStart.DayOfWeek);
    }

    [Theory]
    [InlineData(-1, 2026, 10, 5)]
    [InlineData(0, 2026, 10, 12)]
    [InlineData(1, 2026, 10, 19)]
    public void Shift_MovesByWholeMondayToSundayWeeks(
        int weekDelta,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var shifted = CalendarWeek.Shift(new DateTime(2026, 10, 15), weekDelta);

        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), shifted);
    }
}
