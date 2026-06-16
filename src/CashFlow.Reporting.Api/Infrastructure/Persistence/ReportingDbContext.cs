using CashFlow.Reporting.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Reporting.Infrastructure.Persistence;

public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options)
    : DbContext(options)
{
    public DbSet<ProjectedTransactionEntity> ProjectedTransactions =>
        Set<ProjectedTransactionEntity>();

    public DbSet<DailySummaryEntity> DailySummaries => Set<DailySummaryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectedTransactionEntity>(entity =>
        {
            entity.ToTable("ProjectedTransactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => x.OccurredOn);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.OccurredOn });
        });

        modelBuilder.Entity<DailySummaryEntity>(entity =>
        {
            entity.ToTable("DailySummaries");
            entity.HasKey(x => new { x.UserId, x.ReportDate });
            entity.Property(x => x.UserId).HasMaxLength(256).IsRequired();
            entity.Property(x => x.TotalDebits).HasPrecision(18, 2);
            entity.Property(x => x.TotalCredits).HasPrecision(18, 2);
        });
    }
}
