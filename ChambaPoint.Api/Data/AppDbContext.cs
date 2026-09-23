using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Worker>(e =>
        {
            e.HasIndex(w => w.UserId).IsUnique();
            e.HasOne(w => w.User)
             .WithOne(u => u.WorkerProfile)
             .HasForeignKey<Worker>(w => w.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.HasOne(r => r.Worker)
             .WithMany(w => w.Reviews)
             .HasForeignKey(r => r.WorkerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Customer)
             .WithMany()
             .HasForeignKey(r => r.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}