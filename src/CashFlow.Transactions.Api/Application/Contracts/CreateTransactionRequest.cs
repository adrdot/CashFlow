using System.ComponentModel.DataAnnotations;

namespace CashFlow.Transactions.Application.Contracts;

public sealed record CreateTransactionRequest
{
    [Required]
    public string Type { get; set; } = "Debit";

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateOnly? TransactionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}
