using System.Security.Claims;

namespace Aspire.CashFlow.ServiceDefaults.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static string? ResolveCashFlowUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? user.FindFirstValue(ClaimTypes.Email)
        ?? user.Identity?.Name;
}
