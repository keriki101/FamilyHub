using FamilyHub.Web.Services;
using Moq;
using Xunit;

namespace FamilyHub.Web.Tests;

public sealed class CalendarServiceTests
{
    private static readonly DateTime Today = new(2026, 10, 15, 8, 0, 0);

    private readonly Mock<ICalendarClock> clock = new();
    private readonly CalendarService service;

    public CalendarServiceTests()
    {
        clock.SetupGet(calendarClock => calendarClock.Now).Returns(Today);
        service = new CalendarService(clock.Object);
    }

    [Fact]
    public void GetPreschoolPickup_AssignsParentWithEarlierJobEnd()
    {
        var (mamma, pappa) = CreateParents();
        var events = new[]
        {
            CreateJob(mamma, Today.AddHours(8), Today.AddHours(15)),
            CreateJob(pappa, Today.AddHours(9), Today.AddHours(17))
        };

        var pickup = service.GetPreschoolPickup([mamma, pappa], events);

        Assert.Equal(PreschoolPickupStatus.Assigned, pickup.Status);
        Assert.Same(mamma, pickup.Person);
        Assert.Equal(Today.AddHours(15), pickup.EndTime);
    }

    [Fact]
    public void GetPreschoolPickup_UsesCurrentDateAfterMidnight()
    {
        var (mamma, pappa) = CreateParents();
        var now = Today;
        clock.SetupGet(calendarClock => calendarClock.Now).Returns(() => now);
        var events = new[]
        {
            CreateJob(mamma, Today.AddHours(8), Today.AddHours(15)),
            CreateJob(pappa, Today.AddHours(9), Today.AddHours(17)),
            CreateJob(mamma, Today.AddDays(1).AddHours(8), Today.AddDays(1).AddHours(17)),
            CreateJob(pappa, Today.AddDays(1).AddHours(9), Today.AddDays(1).AddHours(15))
        };

        var todaysPickup = service.GetPreschoolPickup([mamma, pappa], events);
        now = Today.AddDays(1);
        var tomorrowsPickup = service.GetPreschoolPickup([mamma, pappa], events);

        Assert.Same(mamma, todaysPickup.Person);
        Assert.Same(pappa, tomorrowsPickup.Person);
    }

    [Fact]
    public void GetPreschoolPickup_AssignsPappaWhenHeFinishesEarlier()
    {
        var (mamma, pappa) = CreateParents();
        var events = new[]
        {
            CreateJob(mamma, Today.AddHours(8), Today.AddHours(17)),
            CreateJob(pappa, Today.AddHours(9), Today.AddHours(15))
        };

        var pickup = service.GetPreschoolPickup([mamma, pappa], events);

        Assert.Equal(PreschoolPickupStatus.Assigned, pickup.Status);
        Assert.Same(pappa, pickup.Person);
        Assert.Equal(Today.AddHours(15), pickup.EndTime);
    }

    [Fact]
    public void GetPreschoolPickup_ReportsSameEndTimeWithoutAssigningParent()
    {
        var (mamma, pappa) = CreateParents();
        var events = new[]
        {
            CreateJob(mamma, Today.AddHours(8), Today.AddHours(16)),
            CreateJob(pappa, Today.AddHours(9), Today.AddHours(16))
        };

        var pickup = service.GetPreschoolPickup([mamma, pappa], events);

        Assert.Equal(PreschoolPickupStatus.SameEndTime, pickup.Status);
        Assert.Null(pickup.Person);
        Assert.Equal(Today.AddHours(16), pickup.EndTime);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void GetPreschoolPickup_RequiresBothParentsToHaveAJobToday(bool mammaHasJob, bool pappaHasJob)
    {
        var (mamma, pappa) = CreateParents();
        var events = new List<Event>();
        if (mammaHasJob)
        {
            events.Add(CreateJob(mamma, Today.AddHours(8), Today.AddHours(15)));
        }

        if (pappaHasJob)
        {
            events.Add(CreateJob(pappa, Today.AddHours(9), Today.AddHours(17)));
        }

        var pickup = service.GetPreschoolPickup([mamma, pappa], events);

        Assert.Equal(PreschoolPickupStatus.NoSharedWorkday, pickup.Status);
        Assert.Null(pickup.Person);
        Assert.Null(pickup.EndTime);
    }

    [Fact]
    public void GetPreschoolPickup_IgnoresOtherDatesAndNonWorkEvents()
    {
        var (mamma, pappa) = CreateParents();
        var events = new[]
        {
            CreateJob(mamma, Today.AddDays(-1).AddHours(8), Today.AddDays(-1).AddHours(15)),
            CreateJob(pappa, Today.AddDays(1).AddHours(9), Today.AddDays(1).AddHours(17)),
            new Event { PersonId = mamma.Id, Title = "Kontor", Start = Today.AddHours(8), End = Today.AddHours(12) }
        };

        var pickup = service.GetPreschoolPickup([mamma, pappa], events);

        Assert.Equal(PreschoolPickupStatus.NoSharedWorkday, pickup.Status);
    }

    [Fact]
    public void GetPreschoolPickup_UsesLatestEndWhenAParentHasMultipleWorkEvents()
    {
        var (mamma, pappa) = CreateParents();
        var events = new[]
        {
            CreateJob(mamma, Today.AddHours(8), Today.AddHours(14)),
            CreateJob(mamma, Today.AddHours(14), Today.AddHours(16)),
            CreateJob(pappa, Today.AddHours(9), Today.AddHours(17))
        };

        var pickup = service.GetPreschoolPickup([mamma, pappa], events);

        Assert.Equal(PreschoolPickupStatus.Assigned, pickup.Status);
        Assert.Same(mamma, pickup.Person);
        Assert.Equal(Today.AddHours(16), pickup.EndTime);
    }

    [Fact]
    public void GetPreschoolPickup_ReturnsNoSharedWorkdayWhenEitherParentIsMissing()
    {
        var (mamma, _) = CreateParents();
        var pickup = service.GetPreschoolPickup([mamma], []);

        Assert.Equal(PreschoolPickupStatus.NoSharedWorkday, pickup.Status);
    }

    [Fact]
    public void GetPreschoolPickup_UsesFirstMatchingPersonWhenNamesAreDuplicated()
    {
        var (mamma, pappa) = CreateParents();
        var duplicateMamma = new Person { Id = Guid.NewGuid(), Name = "Mamma" };
        var events = new[]
        {
            CreateJob(mamma, Today.AddHours(8), Today.AddHours(15)),
            CreateJob(pappa, Today.AddHours(9), Today.AddHours(17))
        };

        var pickup = service.GetPreschoolPickup([mamma, duplicateMamma, pappa], events);

        Assert.Equal(PreschoolPickupStatus.Assigned, pickup.Status);
        Assert.Same(mamma, pickup.Person);
    }

    [Fact]
    public void CreateWeeklyOccurrences_CreatesEachSelectedWeekdayUntilInclusive()
    {
        var personId = Guid.NewGuid();
        var mondayAndWednesday = (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Wednesday);
        var occurrences = service.CreateWeeklyOccurrences(
            "Fotboll",
            personId,
            new DateOnly(2026, 10, 19),
            new DateOnly(2026, 10, 28),
            new TimeOnly(17, 30),
            new TimeOnly(18, 30),
            mondayAndWednesday);

        Assert.Collection(
            occurrences,
            monday => AssertOccurrence(monday, new DateTime(2026, 10, 19, 17, 30, 0), "Fotboll", personId),
            wednesday => AssertOccurrence(wednesday, new DateTime(2026, 10, 21, 17, 30, 0), "Fotboll", personId),
            nextMonday => AssertOccurrence(nextMonday, new DateTime(2026, 10, 26, 17, 30, 0), "Fotboll", personId),
            nextWednesday => AssertOccurrence(nextWednesday, new DateTime(2026, 10, 28, 17, 30, 0), "Fotboll", personId));

        Assert.All(occurrences, occurrence => Assert.Equal(occurrences[0].RecurrenceSeriesId, occurrence.RecurrenceSeriesId));
        Assert.Equal(4, occurrences.Select(occurrence => occurrence.Id).Distinct().Count());
        Assert.All(occurrences, occurrence =>
        {
            Assert.Equal(new DateTime(2026, 10, 19), occurrence.RecurrenceStart);
            Assert.Equal(new DateTime(2026, 10, 28), occurrence.RecurrenceUntil);
            Assert.Equal(mondayAndWednesday, occurrence.RecurrenceDays);
        });
    }

    [Fact]
    public void CreateWeeklyOccurrences_PreservesProvidedSeriesId()
    {
        var seriesId = Guid.NewGuid();
        var occurrences = service.CreateWeeklyOccurrences(
            "Jobb",
            Guid.NewGuid(),
            new DateOnly(2026, 10, 19),
            new DateOnly(2026, 10, 19),
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            1 << (int)DayOfWeek.Monday,
            seriesId);

        Assert.Single(occurrences);
        Assert.Equal(seriesId, occurrences[0].RecurrenceSeriesId);
    }

    [Fact]
    public void CreateWeeklyOccurrences_RejectsAnEndDateBeforeTheStartDate()
    {
        var exception = Assert.Throws<ArgumentException>(() => service.CreateWeeklyOccurrences(
            "Fotboll",
            Guid.NewGuid(),
            new DateOnly(2026, 10, 21),
            new DateOnly(2026, 10, 19),
            new TimeOnly(17, 0),
            new TimeOnly(18, 0),
            1 << (int)DayOfWeek.Monday));

        Assert.Equal("endDate", exception.ParamName);
    }

    [Fact]
    public void CreateWeeklyOccurrences_RejectsAnEmptyWeekdaySelection()
    {
        var exception = Assert.Throws<ArgumentException>(() => service.CreateWeeklyOccurrences(
            "Fotboll",
            Guid.NewGuid(),
            new DateOnly(2026, 10, 19),
            new DateOnly(2026, 10, 26),
            new TimeOnly(17, 0),
            new TimeOnly(18, 0),
            0));

        Assert.Equal("recurrenceDays", exception.ParamName);
    }

    private static (Person Mamma, Person Pappa) CreateParents() =>
        (new Person { Id = Guid.NewGuid(), Name = " MAMMA " },
         new Person { Id = Guid.NewGuid(), Name = "pappa" });

    private static Event CreateJob(Person person, DateTime start, DateTime end) =>
        new() { PersonId = person.Id, Title = " Jobb ", Start = start, End = end };

    private static void AssertOccurrence(Event occurrence, DateTime start, string title, Guid personId)
    {
        Assert.Equal(start, occurrence.Start);
        Assert.Equal(start.AddHours(1), occurrence.End);
        Assert.Equal(title, occurrence.Title);
        Assert.Equal(personId, occurrence.PersonId);
    }
}
