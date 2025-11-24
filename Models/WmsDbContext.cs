using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WMS_Demo.Models;

namespace WMS_Demo.Data
{
    /// <summary>
    /// Context của cơ sở dữ liệu cho ứng dụng WMS.
    /// </summary>
    // Kế thừa từ IdentityDbContext để quản lý người dùng và vai trò.
    public class WmsDbContext : IdentityDbContext<ApplicationUser>
    {
        public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options)
        {
        }

        // Khai báo các DbSet cho các bảng dữ liệu.
        public DbSet<Item> Items { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Location> Locations { get; set; }
        
        public DbSet<InboundReceipt> InboundReceipts { get; set; }
        public DbSet<InboundReceiptDetail> InboundReceiptDetails { get; set; }
        
        public DbSet<OutboundReceipt> OutboundReceipts { get; set; }
        public DbSet<OutboundReceiptDetail> OutboundReceiptDetails { get; set; }
        
        public DbSet<InventoryLog> InventoryLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // Cần thiết cho việc cấu hình Identity.

            // Tùy chỉnh tên bảng cho Identity.
            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<IdentityRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
            
            // Sử dụng Fluent API để cấu hình các mối quan hệ nếu cần.
        }
    }
}