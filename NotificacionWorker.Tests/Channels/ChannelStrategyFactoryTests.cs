using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using NotificacionWorker.Channels;

namespace NotificacionWorker.Tests.Channels;

public class ChannelStrategyFactoryTests
{
    [Fact]
    public void GetStrategy_WithValidChannelName_ReturnsCorrectStrategy()
    {
        var mockLogger = new Mock<ILogger<ChannelStrategyFactory>>();
        var mockEmailStrategy = new Mock<IChannelStrategy>();
        mockEmailStrategy.Setup(s => s.ChannelName).Returns("Email");

        var mockSmsStrategy = new Mock<IChannelStrategy>();
        mockSmsStrategy.Setup(s => s.ChannelName).Returns("SMS");

        var strategies = new List<IChannelStrategy>
        {
            mockEmailStrategy.Object,
            mockSmsStrategy.Object
        };

        var factory = new ChannelStrategyFactory(mockLogger.Object, strategies);

        var result = factory.GetStrategy("Email");

        Assert.NotNull(result);
        Assert.Equal("Email", result.ChannelName);
    }

    [Fact]
    public void GetStrategy_IsCaseInsensitive()
    {
        var mockLogger = new Mock<ILogger<ChannelStrategyFactory>>();
        var mockEmailStrategy = new Mock<IChannelStrategy>();
        mockEmailStrategy.Setup(s => s.ChannelName).Returns("Email");

        var strategies = new List<IChannelStrategy> { mockEmailStrategy.Object };
        var factory = new ChannelStrategyFactory(mockLogger.Object, strategies);

        var result = factory.GetStrategy("email");

        Assert.NotNull(result);
        Assert.Equal("Email", result.ChannelName);
    }

    [Fact]
    public void GetStrategy_WithInvalidChannelName_ReturnsNull()
    {
        var mockLogger = new Mock<ILogger<ChannelStrategyFactory>>();
        var strategies = new List<IChannelStrategy>();
        var factory = new ChannelStrategyFactory(mockLogger.Object, strategies);

        var result = factory.GetStrategy("CanalInexistente");

        Assert.Null(result);
    }

    [Fact]
    public void GetStrategiesForEvent_OrdenCompletada_ReturnsEmailAndPush()
    {
        var mockLogger = new Mock<ILogger<ChannelStrategyFactory>>();
        
        var mockEmailStrategy = new Mock<IChannelStrategy>();
        mockEmailStrategy.Setup(s => s.ChannelName).Returns("Email");

        var mockPushStrategy = new Mock<IChannelStrategy>();
        mockPushStrategy.Setup(s => s.ChannelName).Returns("Push");

        var mockSmsStrategy = new Mock<IChannelStrategy>();
        mockSmsStrategy.Setup(s => s.ChannelName).Returns("SMS");

        var strategies = new List<IChannelStrategy>
        {
            mockEmailStrategy.Object,
            mockPushStrategy.Object,
            mockSmsStrategy.Object
        };

        var factory = new ChannelStrategyFactory(mockLogger.Object, strategies);

        var result = factory.GetStrategiesForEvent("ordencompletada").ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.ChannelName == "Email");
        Assert.Contains(result, s => s.ChannelName == "Push");
        Assert.DoesNotContain(result, s => s.ChannelName == "SMS");
    }

    [Fact]
    public void GetStrategiesForEvent_AlertaInicioSesion_ReturnsOnlyPush()
    {
        var mockLogger = new Mock<ILogger<ChannelStrategyFactory>>();
        
        var mockEmailStrategy = new Mock<IChannelStrategy>();
        mockEmailStrategy.Setup(s => s.ChannelName).Returns("Email");

        var mockPushStrategy = new Mock<IChannelStrategy>();
        mockPushStrategy.Setup(s => s.ChannelName).Returns("Push");

        var strategies = new List<IChannelStrategy>
        {
            mockEmailStrategy.Object,
            mockPushStrategy.Object
        };

        var factory = new ChannelStrategyFactory(mockLogger.Object, strategies);

        var result = factory.GetStrategiesForEvent("alertainiciosesion").ToList();

        Assert.Single(result);
        Assert.Equal("Push", result[0].ChannelName);
    }

    [Fact]
    public void GetStrategiesForEvent_UnknownEvent_ReturnsEmptyList()
    {
        var mockLogger = new Mock<ILogger<ChannelStrategyFactory>>();
        var strategies = new List<IChannelStrategy>();
        var factory = new ChannelStrategyFactory(mockLogger.Object, strategies);

        var result = factory.GetStrategiesForEvent("eventodesconocido");

        Assert.Empty(result);
    }
}
