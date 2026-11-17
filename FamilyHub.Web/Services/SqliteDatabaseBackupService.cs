using FamilyHub.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Web.Services;

public sealed class SqliteDatabaseBackupService(
    string databasePath,
    IDbContextFactory<FamilyHubDbContext> dbFactory,
    ILogger<SqliteDatabaseBackupService> logger)
{
    private const long MaxBackupSize = 50 * 1024 * 1024;
    private readonly string databasePath = Path.GetFullPath(databasePath);

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var backupPath = CreateTemporaryPath();
        try
        {
            await BackupDatabaseAsync(databasePath, backupPath, cancellationToken);
            return backupPath;
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            TryDelete(backupPath);
            logger.LogError(exception, "Failed to create a FamilyHub database backup.");
            throw new DatabaseBackupException("Säkerhetskopian kunde inte skapas.", exception);
        }
        catch (OperationCanceledException)
        {
            TryDelete(backupPath);
            throw;
        }
    }

    public async Task RestoreAsync(Stream uploadedBackup, CancellationToken cancellationToken = default)
    {
        var uploadedPath = CreateTemporaryPath();
        string? safetyBackupPath = null;
        try
        {
            await using (var output = new FileStream(
                uploadedPath,
                FileMode.Truncate,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                long totalBytes = 0;
                while (true)
                {
                    var bytesRead = await uploadedBackup.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytes += bytesRead;
                    if (totalBytes > MaxBackupSize)
                    {
                        throw new InvalidDataException("The backup exceeds the 50 MB limit.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                if (totalBytes == 0)
                {
                    throw new InvalidDataException("The backup file is empty.");
                }
            }

            try
            {
                await ValidateBackupAsync(uploadedPath, cancellationToken);
            }
            catch (SqliteException exception)
            {
                throw new InvalidDataException("The selected file is not a valid SQLite database.", exception);
            }

            safetyBackupPath = CreateTemporaryPath();
            await BackupDatabaseAsync(databasePath, safetyBackupPath, cancellationToken);

            try
            {
                await BackupDatabaseAsync(uploadedPath, databasePath, cancellationToken);
                await ValidateBackupAsync(databasePath, cancellationToken);
            }
            catch (Exception restoreException) when (
                restoreException is SqliteException or IOException or InvalidDataException)
            {
                try
                {
                    await BackupDatabaseAsync(safetyBackupPath, databasePath, cancellationToken);
                }
                catch (Exception rollbackException) when (
                    rollbackException is SqliteException or IOException)
                {
                    logger.LogCritical(
                        rollbackException,
                        "Failed to roll back the FamilyHub database after a failed restore.");
                    throw new InvalidOperationException(
                        "Database restore failed and the safety rollback also failed.",
                        new AggregateException(restoreException, rollbackException));
                }

                logger.LogError(restoreException, "Failed to restore a FamilyHub database backup.");
                throw new DatabaseBackupException(
                    "Återställningen misslyckades. Den ursprungliga databasen har behållits.",
                    restoreException);
            }
        }
        catch (DatabaseBackupException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(exception, "Rejected an invalid or incompatible FamilyHub backup.");
            throw new DatabaseBackupException(
                "Filen är inte en giltig säkerhetskopia från den här versionen av FamilyHub.",
                exception);
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            logger.LogError(exception, "Failed to process a FamilyHub database restore.");
            throw new DatabaseBackupException("Säkerhetskopian kunde inte återställas.", exception);
        }
        finally
        {
            TryDelete(uploadedPath);
            if (safetyBackupPath is not null)
            {
                TryDelete(safetyBackupPath);
            }
        }
    }

    private async Task ValidateBackupAsync(string path, CancellationToken cancellationToken)
    {
        var migrations = await GetKnownMigrationIdsAsync(cancellationToken);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var integrityCheck = connection.CreateCommand())
        {
            integrityCheck.CommandText = "PRAGMA integrity_check";
            var result = await integrityCheck.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("SQLite integrity check failed.");
            }
        }

        var requiredTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "Events",
            "People",
            "Settings",
            "__EFMigrationsHistory"
        };
        await using (var tablesCommand = connection.CreateCommand())
        {
            tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            await using var reader = await tablesCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                requiredTables.Remove(reader.GetString(0));
            }
        }

        if (requiredTables.Count > 0)
        {
            throw new InvalidDataException("The backup is missing required FamilyHub tables.");
        }

        var requiredColumns = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Events"] =
            [
                "Id", "PersonId", "Title", "Start", "End", "Color",
                "RecurrenceSeriesId", "RecurrenceStart", "RecurrenceUntil", "RecurrenceDays",
                "RecurrenceOccurrenceDate", "RecurrenceIsDeleted", "RecurrenceIsOverride", "IsWorkEvent"
            ],
            ["People"] = ["Id", "Name", "Color"],
            ["Settings"] = ["Id", "PickupPersonOneId", "PickupPersonTwoId"]
        };
        foreach (var (table, columns) in requiredColumns)
        {
            if (!await HasColumnsAsync(connection, table, columns, cancellationToken))
            {
                throw new InvalidDataException($"The backup has an incompatible {table} table.");
            }
        }

        var appliedMigrations = new HashSet<string>(StringComparer.Ordinal);
        await using (var migrationsCommand = connection.CreateCommand())
        {
            migrationsCommand.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory";
            await using var reader = await migrationsCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                appliedMigrations.Add(reader.GetString(0));
            }
        }

        if (!migrations.SetEquals(appliedMigrations))
        {
            throw new InvalidDataException("The backup was created by a different version of FamilyHub.");
        }

        await using var foreignKeysCheck = connection.CreateCommand();
        foreignKeysCheck.CommandText = "PRAGMA foreign_key_check";
        await using var foreignKeyReader = await foreignKeysCheck.ExecuteReaderAsync(cancellationToken);
        if (await foreignKeyReader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException("The backup contains invalid event/person references.");
        }
    }

    private async Task<HashSet<string>> GetKnownMigrationIdsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<bool> HasColumnsAsync(
        SqliteConnection connection,
        string table,
        IEnumerable<string> requiredColumns,
        CancellationToken cancellationToken)
    {
        var remainingColumns = requiredColumns.ToHashSet(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            remainingColumns.Remove(reader.GetString(1));
        }

        return remainingColumns.Count == 0;
    }

    private static async Task BackupDatabaseAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        await using var source = new SqliteConnection(sourceConnectionString);
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection(destinationConnectionString);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private string CreateTemporaryPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"familyhub-{Guid.NewGuid():N}.db");
        using (File.Create(path))
        {
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return path;
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Could not remove temporary FamilyHub database file.");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Could not remove temporary FamilyHub database file.");
        }
    }
}

public sealed class DatabaseBackupException(string message, Exception? innerException = null)
    : Exception(message, innerException);
