using CashFlow.Transactions.Infrastructure.EventStore;

namespace CashFlow.Transactions.UnitTests.EventStore;

public sealed class IdempotencyEventIdTests
{
    [Fact]
    public void Create_IsDeterministic_ForSameUserAndKey()
    {
        var first = IdempotencyEventId.Create("user@cashflow.local", "idem-key-1");
        var second = IdempotencyEventId.Create("user@cashflow.local", "idem-key-1");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_Differs_ForDifferentKeysOrUsers()
    {
        var userA = IdempotencyEventId.Create("user-a", "same-key");
        var userB = IdempotencyEventId.Create("user-b", "same-key");
        var otherKey = IdempotencyEventId.Create("user-a", "other-key");

        Assert.NotEqual(userA, userB);
        Assert.NotEqual(userA, otherKey);
    }
}
