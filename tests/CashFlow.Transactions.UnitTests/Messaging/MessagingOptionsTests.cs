using CashFlow.Transactions.Infrastructure.Messaging;

namespace CashFlow.Transactions.UnitTests.Messaging;

public sealed class MessagingOptionsTests
{
    [Fact]
    public void MessagingOptions_HasExpectedDefaults()
    {
        var options = new MessagingOptions();

        Assert.Equal(5, options.MaxPublishRetries);
        Assert.True(options.Enabled);
    }
}
