namespace CashFlow.Transactions.ContractTests.Infrastructure;

internal static class PactConstants
{
    public const string ConsumerName = "CashFlow.Web";

    public const string ProviderName = "CashFlow.Transactions.Api";

    public static string PactFileName => $"{ConsumerName}-{ProviderName}.json";

    public static string PactDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "pacts"));

    public static string PactFilePath =>
        Path.Combine(PactDirectory, PactFileName);
}
