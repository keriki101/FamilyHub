using FamilyHub.Web.Data;
using FamilyHub.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FamilyHub.Web.Tests;

public sealed class SqliteDatabaseBackupServiceTests
{
    [Fact]
    public async Task RestoreAsync_RestoresAValidFamilyHubBackup()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var backupService = testDatabase.CreateBackupService();
        var backupPath = await backupService.CreateBackupAsync();
        try
        {
            await using (var db = await testDatabase.Factory.CreateDbContextAsync())
            {
                var calendarEvent = await db.Events.SingleAsync();
                calendarEvent.Title = "Changed after backup";
                await db.SaveChangesAsync();
            }

            await using var backupStream = File.OpenRead(backupPath);
            await backupService.RestoreAsync(backupStream);

            await using var verificationDb = await testDatabase.Factory.CreateDbContextAsync();
            var restoredEvent = await verificationDb.Events.SingleAsync();
            Assert.Equal("Original event", restoredEvent.Title);
        }
        finally
        {
            File.Delete(backupPath);
        }
    }

    [Fact]
    public async Task RestoreAsync_RejectsAnInvalidDatabaseWithoutChangingCurrentData()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var invalidBackupPath = Path.Combine(testDatabase.DirectoryPath, "invalid.db");
        await using (var connection = new SqliteConnection($"Data Source={invalidBackupPath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE NotFamilyHub (Id INTEGER PRIMARY KEY)";
            await command.ExecuteNonQueryAsync();
        }

        var backupService = testDatabase.CreateBackupService();
        await using var invalidBackup = File.OpenRead(invalidBackupPath);

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(
            () => backupService.RestoreAsync(invalidBackup));

        Assert.Contains("giltig säkerhetskopia", exception.Message, StringComparison.OrdinalIgnoreCase);
        await using var verificationDb = await testDatabase.Factory.CreateDbContextAsync();
        Assert.Equal("Original event", (await verificationDb.Events.SingleAsync()).Title);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly ServiceProvider serviceProvider;

        private TestDatabase(string directoryPath, ServiceProvider serviceProvider)
        {
            DirectoryPath = directoryPath;
            this.serviceProvider = serviceProvider;
            Factory = serviceProvider.GetRequiredService<IDbContextFactory<FamilyHubDbContext>>();
        }

        public string DirectoryPath { get; }
        public IDbContextFactory<FamilyHubDbContext> Factory { get; }
        private string DatabasePath => Path.Combine(DirectoryPath, "familyhub.db");

        public static async Task<TestDatabase> CreateAsync()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"familyhub-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var connectionString = $"Data Source={Path.Combine(directoryPath, "familyhub.db")}";
            var services = new ServiceCollection();
            services.AddDbContextFactory<FamilyHubDbContext>(
                options => options.UseSqlite(connectionString));
            var testDatabase = new TestDatabase(directoryPath, services.BuildServiceProvider());

            await using var db = await testDatabase.Factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
            var person = new Person { Id = Guid.NewGuid(), Name = "Alex", Color = "#123456" };
            db.People.Add(person);
            db.Events.Add(new Event
            {
                Id = Guid.NewGuid(),
                PersonId = person.Id,
                Title = "Original event",
                Start = new DateTime(2026, 11, 17, 8, 0, 0),
                End = new DateTime(2026, 11, 17, 9, 0, 0)
            });
            await db.SaveChangesAsync();

            return testDatabase;
        }

        public SqliteDatabaseBackupService CreateBackupService() =>
            new(DatabasePath, Factory, NullLogger<SqliteDatabaseBackupService>.Instance);

        public async ValueTask DisposeAsync()
        {
            await serviceProvider.DisposeAsync();
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
