using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Reporting.Infrastructure.Persistence;

public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=127.0.0.1,1433;Database=reporting-db;User Id=sa;Password=CashFlow@Dev123!;TrustServerCertificate=True;Encrypt=False");
        return new ReportingDbContext(optionsBuilder.Options);
    }
}
