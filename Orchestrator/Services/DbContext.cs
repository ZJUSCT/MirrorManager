using Microsoft.EntityFrameworkCore;
using Orchestrator.DataModels;
using Orchestrator.Utils;

namespace Orchestrator.Services;

public class OrchDbContext : DbContext
{
    public DbSet<SavedInfo> SavedInfos { get; set; }

    public OrchDbContext(DbContextOptions<OrchDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        base.OnConfiguring(builder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<SavedInfo>()
            .OwnsOne(info => info.Artifacts, nb =>
            {
                nb.ToJson();
                nb.OwnsMany(x => x.Value);
            });
    }
}
