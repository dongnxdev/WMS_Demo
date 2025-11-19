using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WMS_Demo.Models;

namespace WMS_Demo.Data
{
    // Kế thừa IdentityDbContext để có sẵn bảng Users, Roles, Claims...
    public class WmsDbContext : IdentityDbContext<ApplicationUser>
    {
        public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options)
        {
        }

        // Khai báo các bảng nghiệp vụ
        public DbSet<Item> Items { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Location> Locations { get; set; }
        
        public DbSet<InboundReceipt> InboundReceipts { get; set; }
        public DbSet<InboundReceiptDetail> InboundReceiptDetails { get; set; }
        
        public DbSet<OutboundReceipt> OutboundReceipts { get; set; }
        public DbSet<OutboundReceiptDetail> OutboundReceiptDetails { get; set; }
        
        public DbSet<InventoryLog> InventoryLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // BẮT BUỘC GỌI dòng này để Identity nó hoạt động

            // Cấu hình thêm nếu cần (ví dụ đổi tên bảng Identity cho đỡ ngứa mắt)
            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<IdentityRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
            
            // Fluent API cấu hình quan hệ nếu Attribute chưa đủ đô (tạm thời Attribute ở trên là đủ rồi)
        }
    }
}