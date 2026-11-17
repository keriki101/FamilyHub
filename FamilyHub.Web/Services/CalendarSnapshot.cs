namespace FamilyHub.Web.Services;

public sealed record CalendarSnapshot(
    List<Person> People,
    List<Event> Events,
    FamilyHubSettings Settings);
