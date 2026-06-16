using System.ComponentModel.DataAnnotations;

namespace CashFlow.Auth.Application.Contracts;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? MfaCode { get; set; }

    public string? ChallengeSession { get; set; }

    public string? ChallengeName { get; set; }
}
