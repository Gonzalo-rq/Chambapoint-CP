using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

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

        modelBuilder.Entity<Request>(e =>
        {
            e.HasOne(r => r.Customer)
             .WithMany()
             .HasForeignKey(r => r.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Worker)
             .WithMany()
             .HasForeignKey(r => r.WorkerId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.HasOne(m => m.Sender)
             .WithMany()
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Receiver)
             .WithMany()
             .HasForeignKey(m => m.ReceiverId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Request)
             .WithMany()
             .HasForeignKey(m => m.RequestId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(m => new { m.SenderId, m.ReceiverId });
            e.HasIndex(m => m.SentAt);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.Request)
             .WithMany()
             .HasForeignKey(a => a.RequestId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Worker)
             .WithMany()
             .HasForeignKey(a => a.WorkerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Customer)
             .WithMany()
             .HasForeignKey(a => a.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(a => a.WorkerId);
            e.HasIndex(a => a.CustomerId);
            e.HasIndex(a => a.DateTime);
            e.HasIndex(a => a.Status);
        });
    }
}