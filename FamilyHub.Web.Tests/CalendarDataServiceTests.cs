using FamilyHub.Web.Data;
using FamilyHub.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FamilyHub.Web.Tests;

public sealed class CalendarDataServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsPersistedChangesMadeByAnotherContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContextFactory<FamilyHubDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<FamilyHubDbContext>>();

        var firstPerson = new Person { Id = Guid.NewGuid(), Name = "Mamma", Color = "#123456" };
        var calendarEvent = new Event
        {
            Id = Guid.NewGuid(),
            PersonId = firstPerson.Id,
            Title = "Jobb",
            Start = new DateTime(2026, 10, 15, 8, 0, 0),
            End = new DateTime(2026, 10, 15, 16, 0, 0)
        };
        await using (var setupDb = await factory.CreateDbContextAsync())
        {
            await setupDb.Database.EnsureCreatedAsync();
            setupDb.People.Add(firstPerson);
            setupDb.Events.Add(calendarEvent);
            await setupDb.SaveChangesAsync();
        }

        await using (var otherDeviceDb = await factory.CreateDbContextAsync())
        {
            var updatedEvent = await otherDeviceDb.Events.SingleAsync(item => item.Id == calendarEvent.Id);
            updatedEvent.Title = "Kontor";
            otherDeviceDb.People.Add(new Person { Id = Guid.NewGuid(), Name = "Pappa", Color = "#654321" });
            await otherDeviceDb.SaveChangesAsync();
        }

        var snapshot = await new CalendarDataService(factory).LoadAsync();

        Assert.Collection(
            snapshot.People.OrderBy(person => person.Name),
            mamma => Assert.Equal("Mamma", mamma.Name),
            pappa => Assert.Equal("Pappa", pappa.Name));
        var refreshedEvent = Assert.Single(snapshot.Events);
        Assert.Equal("Kontor", refreshedEvent.Title);
        Assert.Equal(calendarEvent.Id, refreshedEvent.Id);
    }
}
