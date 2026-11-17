using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Web.Data;

public sealed class FamilyHubDbContext(DbContextOptions<FamilyHubDbContext> options)
    : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<FamilyHubSettings> Settings => Set<FamilyHubSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasIndex(calendarEvent => calendarEvent.RecurrenceSeriesId);
    }
}
