using CashFlow.Auth.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace CashFlow.Auth.Infrastructure.Observability;

public sealed class SecurityAuditService(ILogger<SecurityAuditService> logger)
{
    public void Record(SecurityEventRecord securityEvent)
    {
        logger.LogInformation(
            "Security event {EventType} outcome {Outcome} subject {Subject} at {OccurredAtUtc}",
            securityEvent.EventType,
            securityEvent.Outcome,
            securityEvent.Subject,
            securityEvent.OccurredAtUtc
        );
    }
}
