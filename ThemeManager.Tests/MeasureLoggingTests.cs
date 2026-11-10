using Microsoft.Extensions.Logging;
using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;

namespace ThemeManager.Tests;

public sealed class MeasureLoggingTests
{
    [Fact]
    public void CreatingAMeasureDoesNotLogCredentialTarget()
    {
        var logger = new CaptureLogger();
        var measure = MeasureFactory.Create(new MeasureDefinition
        {
            Name = "Test", Type = MeasureType.VibeTrackTitle,
            Target = "test-user|never-log-this-password|calm"
        }, logger);
        try
        {
            Assert.NotEmpty(logger.Messages);
            Assert.All(logger.Messages, message =>
            {
                Assert.DoesNotContain("test-user", message);
                Assert.DoesNotContain("never-log-this-password", message);
            });
        }
        finally { (measure as IDisposable)?.Dispose(); }
    }

    private sealed class CaptureLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
