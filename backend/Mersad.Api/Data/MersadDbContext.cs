using Microsoft.EntityFrameworkCore;
using Mersad.Api.Models;

namespace Mersad.Api.Data;

public class MersadDbContext : DbContext
{
    public MersadDbContext(DbContextOptions<MersadDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();
    public DbSet<NetworkEvent> NetworkEvents => Set<NetworkEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.AgentId)
            .IsUnique();

        modelBuilder.Entity<ProcessEvent>()
            .HasOne<Device>()
            .WithMany(d => d.ProcessEvents)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkEvent>()
            .HasOne<Device>()
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
