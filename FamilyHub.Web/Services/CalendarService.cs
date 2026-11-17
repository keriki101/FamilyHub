namespace FamilyHub.Web.Services;

public enum PreschoolPickupStatus
{
    NoSharedWorkday,
    SameEndTime,
    Assigned
}

public sealed record PreschoolPickup(PreschoolPickupStatus Status, Person? Person, DateTime? EndTime);

public sealed class CalendarService(ICalendarClock clock)
{
    public PreschoolPickup GetPreschoolPickup(IEnumerable<Person> people, IEnumerable<Event> events)
    {
        var mamma = people.FirstOrDefault(person =>
            string.Equals(person.Name.Trim(), "Mamma", StringComparison.OrdinalIgnoreCase));
        var pappa = people.FirstOrDefault(person =>
            string.Equals(person.Name.Trim(), "Pappa", StringComparison.OrdinalIgnoreCase));
        if (mamma is null || pappa is null)
        {
            return new(PreschoolPickupStatus.NoSharedWorkday, null, null);
        }

        var jobEnds = events
            .Where(calendarEvent => calendarEvent.Start.Date == clock.Now.Date
                && string.Equals(calendarEvent.Title.Trim(), "jobb", StringComparison.OrdinalIgnoreCase))
            .GroupBy(calendarEvent => calendarEvent.PersonId)
            .ToDictionary(group => group.Key, group => group.Max(calendarEvent => calendarEvent.End));

        if (!jobEnds.TryGetValue(mamma.Id, out var mammaEnd)
            || !jobEnds.TryGetValue(pappa.Id, out var pappaEnd))
        {
            return new(PreschoolPickupStatus.NoSharedWorkday, null, null);
        }

        if (mammaEnd == pappaEnd)
        {
            return new(PreschoolPickupStatus.SameEndTime, null, mammaEnd);
        }

        return mammaEnd < pappaEnd
            ? new(PreschoolPickupStatus.Assigned, mamma, mammaEnd)
            : new(PreschoolPickupStatus.Assigned, pappa, pappaEnd);
    }

    public List<Event> CreateWeeklyOccurrences(
        string title,
        Guid personId,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly startTime,
        TimeOnly endTime,
        int recurrenceDays,
        Guid? seriesId = null)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("The recurrence end date must not be before its start date.", nameof(endDate));
        }

        if (recurrenceDays == 0)
        {
            throw new ArgumentException("At least one recurrence weekday must be selected.", nameof(recurrenceDays));
        }

        if (endTime <= startTime)
        {
            throw new ArgumentException("The event end time must be later than its start time.", nameof(endTime));
        }

        var occurrenceSeriesId = seriesId ?? Guid.NewGuid();
        var occurrences = new List<Event>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if ((recurrenceDays & (1 << (int)date.DayOfWeek)) == 0)
            {
                continue;
            }

            occurrences.Add(new Event
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                Title = title,
                Start = date.ToDateTime(startTime),
                End = date.ToDateTime(endTime),
                RecurrenceSeriesId = occurrenceSeriesId,
                RecurrenceStart = startDate.ToDateTime(TimeOnly.MinValue),
                RecurrenceUntil = endDate.ToDateTime(TimeOnly.MinValue),
                RecurrenceDays = recurrenceDays,
                RecurrenceOccurrenceDate = date.ToDateTime(TimeOnly.MinValue)
            });
        }

        return occurrences;
    }

    public List<Event> MergeSeriesOccurrences(
        IEnumerable<Event> existingOccurrences,
        IEnumerable<Event> regeneratedOccurrences)
    {
        var existingByDate = existingOccurrences
            .GroupBy(occurrence => (occurrence.RecurrenceOccurrenceDate ?? occurrence.Start).Date)
            .ToDictionary(group => group.Key, group => group.First());

        return regeneratedOccurrences.Select(regenerated =>
        {
            var occurrenceDate = (regenerated.RecurrenceOccurrenceDate ?? regenerated.Start).Date;
            if (!existingByDate.TryGetValue(occurrenceDate, out var existing))
            {
                return regenerated;
            }

            if (!existing.RecurrenceIsOverride && !existing.RecurrenceIsDeleted)
            {
                regenerated.Id = existing.Id;
                return regenerated;
            }

            return new Event
            {
                Id = existing.Id,
                PersonId = existing.PersonId,
                Title = existing.Title,
                Start = existing.Start,
                End = existing.End,
                Color = existing.Color,
                RecurrenceSeriesId = regenerated.RecurrenceSeriesId,
                RecurrenceStart = regenerated.RecurrenceStart,
                RecurrenceUntil = regenerated.RecurrenceUntil,
                RecurrenceDays = regenerated.RecurrenceDays,
                RecurrenceOccurrenceDate = occurrenceDate,
                RecurrenceIsOverride = existing.RecurrenceIsOverride,
                RecurrenceIsDeleted = existing.RecurrenceIsDeleted
            };
        }).ToList();
    }
}
