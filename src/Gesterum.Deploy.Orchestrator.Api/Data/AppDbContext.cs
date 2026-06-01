using Gesterum.Deploy.Orchestrator.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Gesterum.Deploy.Orchestrator.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DeployJob> Jobs => Set<DeployJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeployJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.JobType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasMaxLength(40).IsRequired();
            e.Property(x => x.PayloadJson).IsRequired();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
