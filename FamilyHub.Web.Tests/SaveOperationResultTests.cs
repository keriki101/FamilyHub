using FamilyHub.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FamilyHub.Web.Tests;

public sealed class SaveOperationResultTests
{
    [Fact]
    public async Task TrySaveAsync_ReturnsFailureAndLogsDatabaseUpdateErrors()
    {
        var logger = new Mock<ILogger>();

        var result = await SaveOperationResult.TrySaveAsync(
            () => Task.FromException(new DbUpdateException("Database write failed.")),
            logger.Object,
            "calendar event");

        Assert.False(result.Succeeded);
        Assert.Contains("försök igen", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        logger.Verify(
            item => item.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("calendar event")),
                It.IsAny<DbUpdateException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TrySaveAsync_PropagatesUnexpectedErrors()
    {
        var logger = new Mock<ILogger>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SaveOperationResult.TrySaveAsync(
                () => Task.FromException(new InvalidOperationException("Unexpected failure.")),
                logger.Object,
                "calendar event"));
    }

    [Fact]
    public async Task TrySaveAsync_ReturnsSuccessWhenWriteCompletes()
    {
        var logger = new Mock<ILogger>();

        var result = await SaveOperationResult.TrySaveAsync(
            () => Task.CompletedTask,
            logger.Object,
            "calendar event");

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }
}
