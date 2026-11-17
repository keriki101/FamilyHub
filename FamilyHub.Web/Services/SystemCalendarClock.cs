namespace FamilyHub.Web.Services;

public sealed class SystemCalendarClock : ICalendarClock
{
    public DateTime Now => DateTime.Now;
}
