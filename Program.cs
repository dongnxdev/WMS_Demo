
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Data;
using WMS_Demo.Models;

var builder = WebApplication.CreateBuilder(args);

// Lấy chuỗi kết nối cơ sở dữ liệu từ cấu hình.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Đăng ký WmsDbContext với SQL Server.
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseSqlServer(connectionString));

// Cấu hình hệ thống Identity cho việc quản lý người dùng và vai trò.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<WmsDbContext>()
    .AddDefaultTokenProviders();

// Đăng ký các dịch vụ cho Controllers và Views.
builder.Services.AddControllersWithViews();

// Đăng ký Swagger/OpenAPI để tạo tài liệu và kiểm thử API.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Cấu hình pipeline xử lý HTTP request.

// Cấu hình Swagger UI chỉ trong môi trường phát triển.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Thêm await vào đây vì hàm Initialize giờ là async
        await WMS_Demo.Data.DbInitializer.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi sấp mặt khi seed data. Check lại đi đại ca.");
    }
}
}

app.UseHttpsRedirection();
app.UseStaticFiles();
// Kích hoạt middleware xác thực và phân quyền.
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();