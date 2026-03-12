using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using NotificacionWorker.Channels;
using NotificacionWorker.Models;
using NotificacionWorker.Services;

namespace NotificacionWorker.Tests.Services;

public class NotificationOrchestratorTests
{
    private readonly Mock<ILogger<NotificationOrchestrator>> _mockLogger;
    private readonly Mock<IChannelStrategyFactory> _mockChannelFactory;
    private readonly NotificationOrchestrator _orchestrator;

    public NotificationOrchestratorTests()
    {
        _mockLogger = new Mock<ILogger<NotificationOrchestrator>>();
        _mockChannelFactory = new Mock<IChannelStrategyFactory>();
        _orchestrator = new NotificationOrchestrator(_mockLogger.Object, _mockChannelFactory.Object);
    }

    [Fact]
    public async Task RouteNotificationAsync_WithValidRequest_CallsAppropriateStrategies()
    {
        var request = new NotificationRequest
        {
            EventType = "ordencompletada",
            Data = new Dictionary<string, object> { { "orderId", "123" } },
            Timestamp = DateTime.UtcNow
        };

        var mockEmailStrategy = new Mock<IChannelStrategy>();
        mockEmailStrategy.Setup(s => s.ChannelName).Returns("Email");
        mockEmailStrategy.Setup(s => s.ProcessAndPublishAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockPushStrategy = new Mock<IChannelStrategy>();
        mockPushStrategy.Setup(s => s.ChannelName).Returns("Push");
        mockPushStrategy.Setup(s => s.ProcessAndPublishAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var strategies = new List<IChannelStrategy> { mockEmailStrategy.Object, mockPushStrategy.Object };

        _mockChannelFactory
            .Setup(f => f.GetStrategiesForEvent("ordencompletada"))
            .Returns(strategies);

        await _orchestrator.RouteNotificationAsync(request);

        mockEmailStrategy.Verify(s => s.ProcessAndPublishAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        mockPushStrategy.Verify(s => s.ProcessAndPublishAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RouteNotificationAsync_WithNullRequest_LogsWarningAndReturns()
    {
        await _orchestrator.RouteNotificationAsync(null!);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockChannelFactory.Verify(f => f.GetStrategiesForEvent(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RouteNotificationAsync_WithNoStrategiesAvailable_LogsWarning()
    {
        var request = new NotificationRequest
        {
            EventType = "eventotestdesconocido",
            Data = new Dictionary<string, object>(),
            Timestamp = DateTime.UtcNow
        };

        _mockChannelFactory
            .Setup(f => f.GetStrategiesForEvent("eventotestdesconocido"))
            .Returns(Enumerable.Empty<IChannelStrategy>());

        await _orchestrator.RouteNotificationAsync(request);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No hay canales")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteNotificationAsync_WhenStrategyFails_ContinuesWithOtherStrategies()
    {
        var request = new NotificationRequest
        {
            EventType = "ordencompletada",
            Data = new Dictionary<string, object>(),
            Timestamp = DateTime.UtcNow
        };

        var mockEmailStrategy = new Mock<IChannelStrategy>();
        mockEmailStrategy.Setup(s => s.ChannelName).Returns("Email");
        mockEmailStrategy.Setup(s => s.ProcessAndPublishAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error simulado en Email"));

        var mockPushStrategy = new Mock<IChannelStrategy>();
        mockPushStrategy.Setup(s => s.ChannelName).Returns("Push");
        mockPushStrategy.Setup(s => s.ProcessAndPublishAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var strategies = new List<IChannelStrategy> { mockEmailStrategy.Object, mockPushStrategy.Object };

        _mockChannelFactory
            .Setup(f => f.GetStrategiesForEvent("ordencompletada"))
            .Returns(strategies);

        await _orchestrator.RouteNotificationAsync(request);

        mockEmailStrategy.Verify(s => s.ProcessAndPublishAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        mockPushStrategy.Verify(s => s.ProcessAndPublishAsync(request, It.IsAny<CancellationToken>()), Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error enviando")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
