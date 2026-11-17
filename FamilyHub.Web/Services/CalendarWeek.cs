namespace FamilyHub.Web.Services;

public static class CalendarWeek
{
    public static DateTime GetStart(DateTime date)
    {
        var dayOffset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-dayOffset);
    }

    public static DateTime Shift(DateTime weekStart, int numberOfWeeks) =>
        GetStart(weekStart).AddDays(numberOfWeeks * 7);
}
