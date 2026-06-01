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
            e.Property(x => x.RequestedBy).HasMaxLength(120).IsRequired();
            e.Property(x => x.Environment).HasMaxLength(30);
            e.Property(x => x.Runtime).HasMaxLength(30);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
