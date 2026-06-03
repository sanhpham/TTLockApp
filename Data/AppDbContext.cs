using Microsoft.EntityFrameworkCore;
using TTLockManager.Models;

namespace TTLockManager.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerCredential> CustomerCredentials { get; set; }
        public DbSet<WashRecord> WashRecords { get; set; }
        public DbSet<MonthlyUsage> MonthlyUsages { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<DeviceLock> DeviceLocks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Index để tránh đếm trùng record từ TTLock
            modelBuilder.Entity<WashRecord>()
                .HasIndex(r => r.TTLockRecordId)
                .IsUnique();

            // Index tìm kiếm nhanh theo tháng/năm
            modelBuilder.Entity<MonthlyUsage>()
                .HasIndex(m => new { m.CustomerId, m.Month, m.Year })
                .IsUnique();

            // Index tìm nhanh credential theo keyboardPwd
            modelBuilder.Entity<CustomerCredential>()
                .HasIndex(c => c.CredentialId);
        }
    }
}
