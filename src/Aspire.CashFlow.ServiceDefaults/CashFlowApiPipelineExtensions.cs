using Aspire.CashFlow.ServiceDefaults.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Aspire.CashFlow.ServiceDefaults;

public static class CashFlowApiPipelineExtensions
{
    public static WebApplication UseCashFlowApiPipeline(
        this WebApplication app,
        bool useCashFlowHttpsRedirection = true
    )
    {
        app.UseExceptionHandler();

        if (useCashFlowHttpsRedirection)
        {
            app.UseCashFlowHttpsRedirection();
        }
        else
        {
            app.UseHttpsRedirection();
        }

        app.UseCashFlowSecurity();
        return app;
    }

    public static WebApplication UseCashFlowApiAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
