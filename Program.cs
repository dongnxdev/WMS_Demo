
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Data;
using WMS_Demo.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Lấy chuỗi kết nối từ appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 2. Đăng ký DbContext (Kết nối SQL Server)
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseSqlServer(connectionString));

// 3. Đăng ký Identity (Quản lý User/Role)
// Quan trọng: Dùng AddIdentityApiEndpoints hoặc cấu hình thủ công. 
// nhận bảng Users/Roles trong DB.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<WmsDbContext>()
    .AddDefaultTokenProviders();

// 4. Đăng ký Controllers (Để viết API)
builder.Services.AddControllers();

// 5. Đăng ký Swagger (Để test API)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- PHẦN MIDDLEWARE (Xử lý Request) ---

// Cấu hình Swagger UI (Chỉ hiện khi chạy ở môi trường Dev)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Kích hoạt Authentication (Xác thực) & Authorization (Phân quyền)
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

app.Run();