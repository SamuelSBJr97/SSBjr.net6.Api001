using Microsoft.EntityFrameworkCore;
using SSBJr.Net6.Api001.Models;

namespace SSBJr.Net6.Api001.Data
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

        // Pending updates will be persisted as files by default; keep DbSet if we want DB-backed approach later
        public DbSet<PendingUpdate>? PendingUpdates => Set<PendingUpdate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(eb =>
            {
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Price).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Notification>(eb =>
            {
                eb.HasKey(e => e.Id);
                // store enum flags as integer
                eb.Property(e => e.Status).HasConversion<int>();
                eb.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<RequestLog>(eb =>
            {
                eb.HasKey(e => e.Id);
            });
        }
    }
}
