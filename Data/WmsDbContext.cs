using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WMS_Demo.Models;

namespace WMS_Demo.Data
{
    /// <summary>
    /// Context cơ sở dữ liệu chính của ứng dụng.
    /// </summary>
    // Kế thừa từ IdentityDbContext để tích hợp quản lý người dùng, vai trò.
    public class WmsDbContext : IdentityDbContext<ApplicationUser>
    {
        public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options)
        {
        }

        // Khai báo các DbSet tương ứng với các bảng trong cơ sở dữ liệu.
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
            base.OnModelCreating(builder); // Bắt buộc gọi phương thức của lớp cơ sở để Identity hoạt động.

            // Tùy chỉnh tên bảng của ASP.NET Core Identity.
            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<IdentityRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
            
            // Cấu hình chi tiết các model và mối quan hệ bằng Fluent API.
        }
    }
}