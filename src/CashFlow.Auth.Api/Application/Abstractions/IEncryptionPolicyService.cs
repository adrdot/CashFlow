namespace CashFlow.Auth.Application.Abstractions;

public interface IEncryptionPolicyService
{
    string GetKeyIdentifier(string purpose);

    bool RequiresCustomerManagedKey(string resourceType);
}