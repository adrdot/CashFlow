namespace CashFlow.Auth.Infrastructure.Security.Abstractions;

public interface IEncryptionPolicyService
{
    string GetKeyIdentifier(string purpose);

    bool RequiresCustomerManagedKey(string resourceType);
}
