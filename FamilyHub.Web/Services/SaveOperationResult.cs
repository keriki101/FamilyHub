using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyHub.Web.Services;

public sealed record SaveOperationResult(bool Succeeded, string? ErrorMessage = null)
{
    public static SaveOperationResult Success { get; } = new(true);

    public static SaveOperationResult Failure(string errorMessage) => new(false, errorMessage);

    public static async Task<SaveOperationResult> TrySaveAsync(
        Func<Task> operation,
        ILogger logger,
        string operationName)
    {
        try
        {
            await operation();
            return Success;
        }
        catch (Exception exception) when (exception is DbUpdateException or SqliteException)
        {
            logger.LogError(exception, "Failed to save {Operation} to SQLite.", operationName);
            return Failure("Kunde inte spara ändringen. Kontrollera att databasen är tillgänglig och försök igen.");
        }
    }
}
