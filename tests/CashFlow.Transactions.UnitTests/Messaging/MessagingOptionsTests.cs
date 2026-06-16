using CashFlow.Transactions.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Transactions.UnitTests.Messaging;

public sealed class MessagingOptionsTests
{
    [Fact]
    public void MessagingOptions_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Messaging:MaxPublishRetries"] = "5",
                    ["Messaging:Enabled"] = "false",
                    ["Messaging:Region"] = "us-east-1",
                }
            )
            .Build();

        var options =
            configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>()
            ?? new MessagingOptions();

        Assert.Equal(5, options.MaxPublishRetries);
        Assert.False(options.Enabled);
        Assert.Equal("us-east-1", options.Region);
    }
}
