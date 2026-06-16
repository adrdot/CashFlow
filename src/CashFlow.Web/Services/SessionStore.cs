using Microsoft.JSInterop;

namespace CashFlow.Web.Services;

public sealed class SessionStore(IJSRuntime jsRuntime)
{
    private const string StorageKey = "cashflow.auth.session";

    public async Task<StoredSession?> LoadAsync()
    {
        return await jsRuntime.InvokeAsync<StoredSession?>("cashFlowAuthStorage.get", StorageKey);
    }

    public async Task SaveAsync(StoredSession session)
    {
        await jsRuntime.InvokeVoidAsync("cashFlowAuthStorage.set", StorageKey, session);
    }

    public async Task ClearAsync()
    {
        await jsRuntime.InvokeVoidAsync("cashFlowAuthStorage.remove", StorageKey);
    }
}
