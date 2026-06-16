using CashFlow.Transactions.ContractTests.Infrastructure;
using PactNet.Infrastructure.Outputters;
using PactNet.Output.Xunit;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace CashFlow.Transactions.ContractTests;

[Collection("PactProvider")]
public sealed class CreateTransactionProviderPactTests : IClassFixture<TransactionsPactHostFixture>
{
    private readonly TransactionsPactHostFixture fixture;
    private readonly ITestOutputHelper output;

    public CreateTransactionProviderPactTests(TransactionsPactHostFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    [Fact]
    public void TransactionsApi_HonoursPactWithCashFlowWeb()
    {
        var pactFile = new FileInfo(PactConstants.PactFilePath);
        Assert.True(pactFile.Exists, $"Pact file not found at {pactFile.FullName}. Run consumer pact tests first.");

        var config = new PactVerifierConfig
        {
            Outputters = new List<IOutput> { new XunitOutput(output) }
        };

        using var verifier = new PactVerifier(PactConstants.ProviderName, config);

        verifier
            .WithHttpEndpoint(fixture.ServerUri)
            .WithFileSource(pactFile)
            .WithProviderStateUrl(new Uri(fixture.ServerUri, "/provider-states"))
            .Verify();
    }
}

[CollectionDefinition("PactProvider", DisableParallelization = true)]
public sealed class PactProviderCollection : ICollectionFixture<TransactionsPactHostFixture>;
