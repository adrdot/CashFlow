using BenchmarkDotNet.Attributes;
using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Application.UseCases;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Infrastructure.Persistence;

namespace CashFlow.Transactions.Benchmarks;

[MemoryDiagnoser]
public class CreateTransactionHandlerBenchmarks
{
    private CreateTransactionHandler handler = null!;
    private CreateTransactionRequest request = null!;

    [GlobalSetup]
    public void Setup()
    {
        handler = new CreateTransactionHandler(new InMemoryTransactionRepository());
        request = new CreateTransactionRequest
        {
            Type = "Credit",
            Amount = 199.99m,
            Description = "Benchmark salary deposit",
            TransactionDate = new DateOnly(2026, 6, 14)
        };
    }

    [Benchmark(Description = "CreateAsync valid debit/credit request")]
    public async Task<CreateTransactionResult> CreateAsync_ValidRequest()
        => await handler.CreateAsync(request, "benchmark-user@cashflow.local");
}
