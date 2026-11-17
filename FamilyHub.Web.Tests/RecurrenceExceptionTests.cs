using FamilyHub.Web.Data;
using FamilyHub.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FamilyHub.Web.Tests;

public sealed class RecurrenceExceptionTests
{
    [Fact]
    public async Task Migration_BackfillsOccurrenceDatesForExistingRecurringEvents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<FamilyHubDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new FamilyHubDbContext(options);

        await db.Database.MigrateAsync("20261015181500_EventRecurrenceSeriesIndex");
        var personId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var start = new DateTime(2026, 10, 15, 8, 0, 0);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO People (Id, Name, Color) VALUES ({personId.ToString()}, {"Mamma"}, {"#123456"})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Events (Id, PersonId, Title, Start, End, Color, RecurrenceSeriesId, RecurrenceStart, RecurrenceUntil, RecurrenceDays) VALUES ({eventId.ToString()}, {personId.ToString()}, {"Fotboll"}, {start.ToString("yyyy-MM-dd HH:mm:ss")}, {start.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss")}, {""}, {seriesId.ToString()}, {start.Date.ToString("yyyy-MM-dd HH:mm:ss")}, {start.Date.AddMonths(1).ToString("yyyy-MM-dd HH:mm:ss")}, {1 << (int)DayOfWeek.Thursday})");
        var beforeMigration = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM Events").SingleAsync();
        Assert.Equal(1, beforeMigration);
        await db.Database.MigrateAsync();

        var migrated = Assert.Single(await db.Events.ToListAsync());
        Assert.Equal(eventId, migrated.Id);
        Assert.Equal(start.Date, migrated.RecurrenceOccurrenceDate);
        Assert.False(migrated.RecurrenceIsOverride);
        Assert.False(migrated.RecurrenceIsDeleted);
    }

    [Fact]
    public void MergeSeriesOccurrences_PreservesOverridesAndDeletedDatesWhileUpdatingSeries()
    {
        var seriesId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var originalStart = new DateTime(2026, 10, 19);
        var originalSeries = CreateOccurrences(seriesId, personId, "Old title", originalStart);
        var overridden = originalSeries[1];
        overridden.Title = "Dentist";
        overridden.Start = new DateTime(2026, 10, 21, 10, 0, 0);
        overridden.End = new DateTime(2026, 10, 21, 11, 0, 0);
        overridden.RecurrenceIsOverride = true;
        var deleted = originalSeries[2];
        deleted.RecurrenceIsDeleted = true;
        var regenerated = CreateOccurrences(seriesId, personId, "New title", originalStart.AddDays(-7));

        var merged = CreateCalendarService().MergeSeriesOccurrences(originalSeries, regenerated);

        var regeneratedMonday = Assert.Single(merged, item => item.RecurrenceOccurrenceDate == originalStart);
        Assert.Equal(originalSeries[0].Id, regeneratedMonday.Id);
        Assert.Equal("New title", regeneratedMonday.Title);

        var preservedOverride = Assert.Single(merged, item => item.Id == overridden.Id);
        Assert.Equal("Dentist", preservedOverride.Title);
        Assert.Equal(overridden.Start, preservedOverride.Start);
        Assert.True(preservedOverride.RecurrenceIsOverride);
        Assert.Equal(new DateTime(2026, 10, 12), preservedOverride.RecurrenceStart);

        var preservedDeletion = Assert.Single(merged, item => item.Id == deleted.Id);
        Assert.True(preservedDeletion.RecurrenceIsDeleted);
        Assert.Equal(new DateTime(2026, 10, 21), preservedDeletion.RecurrenceOccurrenceDate);
        Assert.DoesNotContain(merged, item => item.RecurrenceOccurrenceDate == new DateTime(2026, 10, 26));
    }

    [Fact]
    public void MergeSeriesOccurrences_DropsExceptionsOutsideTheRevisedSchedule()
    {
        var seriesId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var start = new DateTime(2026, 10, 19);
        var existing = CreateOccurrences(seriesId, personId, "Fotboll", start);
        existing[1].RecurrenceIsDeleted = true;
        existing[2].RecurrenceIsOverride = true;
        var revised = CreateOccurrences(seriesId, personId, "Fotboll", start);
        revised.RemoveAll(item => item.RecurrenceOccurrenceDate > start
            && item.RecurrenceOccurrenceDate <= start.AddDays(2));

        var merged = CreateCalendarService().MergeSeriesOccurrences(existing, revised);

        Assert.DoesNotContain(merged, item => item.RecurrenceOccurrenceDate == start.AddDays(1));
        Assert.DoesNotContain(merged, item => item.RecurrenceOccurrenceDate == start.AddDays(2));
    }

    [Fact]
    public async Task SaveSeriesAsync_PreservesAndHidesDeletedOccurrencesAndKeepsEditedOverrides()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContextFactory<FamilyHubDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<FamilyHubDbContext>>();
        var person = new Person { Id = Guid.NewGuid(), Name = "Mamma", Color = "#123456" };
        var seriesId = Guid.NewGuid();
        var initial = CreateOccurrences(seriesId, person.Id, "Fotboll", new DateTime(2026, 10, 19));

        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.People.Add(person);
            db.Events.AddRange(initial);
            await db.SaveChangesAsync();
        }

        var calendarData = new CalendarDataService(factory);
        var deletedOccurrence = initial[1];
        await calendarData.DeleteOccurrenceAsync(deletedOccurrence);

        var overriddenOccurrence = initial[0];
        overriddenOccurrence.Title = "Tandläkare";
        overriddenOccurrence.Start = new DateTime(2026, 10, 19, 10, 0, 0);
        overriddenOccurrence.End = new DateTime(2026, 10, 19, 11, 0, 0);
        await calendarData.UpdateOccurrenceAsync(overriddenOccurrence);

        var editedSchedule = CreateOccurrences(
            seriesId,
            person.Id,
            "Uppdaterad fotboll",
            new DateTime(2026, 10, 12));
        await calendarData.SaveSeriesAsync(seriesId, editedSchedule, CreateCalendarService());

        var snapshot = await calendarData.LoadAsync();
        Assert.DoesNotContain(snapshot.Events, item => item.RecurrenceOccurrenceDate == new DateTime(2026, 10, 20));
        Assert.Contains(snapshot.Events, item =>
            item.RecurrenceOccurrenceDate == new DateTime(2026, 10, 12)
            && item.Title == "Uppdaterad fotboll");
        var overrideAfterSeriesEdit = Assert.Single(snapshot.Events, item => item.Id == overriddenOccurrence.Id);
        Assert.Equal("Tandläkare", overrideAfterSeriesEdit.Title);
        Assert.Equal(overriddenOccurrence.Start, overrideAfterSeriesEdit.Start);
        Assert.True(overrideAfterSeriesEdit.RecurrenceIsOverride);
        Assert.Equal(new DateTime(2026, 10, 12), overrideAfterSeriesEdit.RecurrenceStart);

        await using var verificationDb = await factory.CreateDbContextAsync();
        var deletedRow = await verificationDb.Events.SingleAsync(item => item.Id == deletedOccurrence.Id);
        Assert.True(deletedRow.RecurrenceIsDeleted);
    }

    private static List<Event> CreateOccurrences(
        Guid seriesId,
        Guid personId,
        string title,
        DateTime startDate) =>
        Enumerable.Range(0, 10)
            .Select(offset => new Event
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                Title = title,
                Start = startDate.AddDays(offset).AddHours(17),
                End = startDate.AddDays(offset).AddHours(18),
                RecurrenceSeriesId = seriesId,
                RecurrenceStart = startDate,
                RecurrenceUntil = startDate.AddDays(9),
                RecurrenceDays = 127,
                RecurrenceOccurrenceDate = startDate.AddDays(offset)
            })
            .ToList();

    private static CalendarService CreateCalendarService() =>
        new(new Mock<ICalendarClock>().Object);
}
