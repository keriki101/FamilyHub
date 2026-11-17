using FamilyHub.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Web.Services;

public sealed class CalendarDataService(IDbContextFactory<FamilyHubDbContext> dbFactory)
{
    public async Task<CalendarSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var people = await db.People
            .AsNoTracking()
            .OrderBy(person => person.Name)
            .ToListAsync(cancellationToken);
        var events = await db.Events
            .AsNoTracking()
            .Where(calendarEvent => !calendarEvent.RecurrenceIsDeleted)
            .OrderBy(calendarEvent => calendarEvent.Start)
            .ToListAsync(cancellationToken);
        var settings = await db.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken)
            ?? new FamilyHubSettings();

        return new CalendarSnapshot(people, events, settings);
    }

    public async Task SaveSettingsAsync(
        FamilyHubSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.PickupPersonOneId is not Guid firstId
            || settings.PickupPersonTwoId is not Guid secondId
            || firstId == secondId)
        {
            throw new ArgumentException("Choose two different pickup people.", nameof(settings));
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var peopleCount = await db.People.CountAsync(
            person => person.Id == firstId || person.Id == secondId,
            cancellationToken);
        if (peopleCount != 2)
        {
            throw new ArgumentException("Both pickup people must exist in the family list.", nameof(settings));
        }

        var current = await db.Settings.SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (current is null)
        {
            settings.Id = 1;
            db.Settings.Add(settings);
        }
        else
        {
            current.PickupPersonOneId = firstId;
            current.PickupPersonTwoId = secondId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOccurrenceAsync(Event updatedEvent, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (updatedEvent.RecurrenceSeriesId is not null)
        {
            updatedEvent.RecurrenceOccurrenceDate ??= updatedEvent.Start.Date;
            updatedEvent.RecurrenceIsOverride = true;
        }

        db.Events.Update(updatedEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteOccurrenceAsync(Event calendarEvent, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Events.SingleAsync(
            item => item.Id == calendarEvent.Id,
            cancellationToken);

        if (existing.RecurrenceSeriesId is not null)
        {
            existing.RecurrenceOccurrenceDate ??= existing.Start.Date;
            existing.RecurrenceIsDeleted = true;
        }
        else
        {
            db.Events.Remove(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveSeriesAsync(
        Guid seriesId,
        IEnumerable<Event> regeneratedOccurrences,
        CalendarService calendarService,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Events
            .Where(calendarEvent => calendarEvent.RecurrenceSeriesId == seriesId)
            .ToListAsync(cancellationToken);
        var regenerated = regeneratedOccurrences.ToList();
        var merged = calendarService.MergeSeriesOccurrences(existing, regenerated);
        var mergedIds = merged.Select(calendarEvent => calendarEvent.Id).ToHashSet();

        db.Events.RemoveRange(existing.Where(calendarEvent => !mergedIds.Contains(calendarEvent.Id)));
        foreach (var calendarEvent in merged)
        {
            var current = existing.FirstOrDefault(item => item.Id == calendarEvent.Id);
            if (current is null)
            {
                db.Events.Add(calendarEvent);
            }
            else
            {
                db.Entry(current).CurrentValues.SetValues(calendarEvent);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSeriesAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var occurrences = await db.Events
            .Where(calendarEvent => calendarEvent.RecurrenceSeriesId == seriesId)
            .ToListAsync(cancellationToken);
        db.Events.RemoveRange(occurrences);
        await db.SaveChangesAsync(cancellationToken);
    }

}
