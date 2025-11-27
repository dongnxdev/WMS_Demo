using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Models;

namespace WMS_Demo.Data
{
    public static class DbInitializer
    {
        // Sử dụng phương thức bất đồng bộ để khởi tạo dữ liệu người dùng.
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<WmsDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Tạo cơ sở dữ liệu nếu chưa tồn tại.
            context.Database.EnsureCreated();

            // Kiểm tra nếu đã có dữ liệu người dùng thì bỏ qua quá trình khởi tạo.
            if (await userManager.Users.AnyAsync())
            {
                Console.WriteLine("Cơ sở dữ liệu đã tồn tại.");
                return; 
            }

            // --- A. TẠO TÀI KHOẢN (NGƯỜI DÙNG) ---
            var adminUser = new ApplicationUser
            {
                UserName = "admin@wms.com",
                Email = "admin@wms.com",
                FullName = "Nguyễn Xuân Đông (Boss)",
                StaffCode = "BOSS-01",
                IsActive = true,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, "Abc@12345"); 

            var staffUser = new ApplicationUser
            {
                UserName = "staff@wms.com",
                Email = "staff@wms.com",
                FullName = "Nhân viên cần cù",
                StaffCode = "STAFF-99",
                IsActive = true,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(staffUser, "Abc@12345");
            
            var userFaker = new Faker<ApplicationUser>()
                .RuleFor(u => u.FullName, f => f.Name.FullName())
                .RuleFor(u => u.Email, f => f.Internet.Email(provider: "wms.com"))
                .RuleFor(u => u.UserName, (f, u) => u.Email) // Đặt UserName giống Email
                .RuleFor(u => u.StaffCode, f => $"STAFF-{f.Random.Number(100, 999)}")
                .RuleFor(u => u.IsActive, true)
                .RuleFor(u => u.EmailConfirmed, true);

            var fakeUsers = userFaker.Generate(5); // Tạo thêm 5 user giả
            foreach (var user in fakeUsers)
            {
                await userManager.CreateAsync(user, "Abc@12345");
            }
        
            
            // Lấy danh sách ID người dùng để gán vào các giao dịch nhập/xuất kho.
            var userIds = fakeUsers.Select(u => u.Id).ToList();

            // --- B. TẠO DỮ LIỆU DANH MỤC (NHÀ CUNG CẤP, KHÁCH HÀNG, KHO, VẬT TƯ) ---
            
            // 1. Nhà cung cấp
            var supplierFaker = new Faker<Supplier>()
                .RuleFor(s => s.Name, f => f.Company.CompanyName())
                .RuleFor(s => s.Address, f => f.Address.FullAddress())
                .RuleFor(s => s.PhoneNumber, f => f.Phone.PhoneNumber("09########"));
            var suppliers = supplierFaker.Generate(10);
            context.Suppliers.AddRange(suppliers);

            // 2. Khách hàng
            var customerFaker = new Faker<Customer>()
                .RuleFor(c => c.Name, f => f.Name.FullName())
                .RuleFor(c => c.Address, f => f.Address.FullAddress())
                .RuleFor(c => c.PhoneNumber, f => f.Phone.PhoneNumber("09########"));
            var customers = customerFaker.Generate(10);
            context.Customers.AddRange(customers);

            // 3. Vị trí kho
            var locations = new List<Location>();
            string[] aisles = { "A", "B", "C" };
            foreach (var aisle in aisles)
            {
                for (int i = 1; i <= 5; i++)
                {
                    locations.Add(new Location { Code = $"{aisle}-{i:D2}", Description = $"Kệ {aisle} tầng {i}" });
                }
            }
            context.Locations.AddRange(locations);

            // 4. Vật tư (Lưu ý: Để tồn kho ban đầu bằng 0, ta sẽ tăng số lượng qua phiếu Nhập kho để đảm bảo tính nhất quán của Log)
            var itemFaker = new Faker<Item>()
                .RuleFor(i => i.Name, f => f.Commerce.ProductName())
                .RuleFor(i => i.Code, f => f.Commerce.Ean8())
                .RuleFor(i => i.Unit, f => f.PickRandom("Cái", "Hộp", "Kg"))
                .RuleFor(i => i.SafetyStock, f => f.Random.Double(10, 20))
                .RuleFor(i => i.CurrentStock, 0); // Bắt đầu bằng 0
            var items = itemFaker.Generate(20);
            context.Items.AddRange(items);

            await context.SaveChangesAsync(); // Lưu lần đầu để có ID dùng cho các bảng quan hệ.

            // --- C. TẠO GIAO DỊCH (NHẬP/XUẤT & NHẬT KÝ KHO) ---
            
            // Lấy danh sách ID đã lưu để tạo dữ liệu ngẫu nhiên.
            var supplierIds = suppliers.Select(s => s.Id).ToList();
            var customerIds = customers.Select(c => c.Id).ToList();
            var locationIds = locations.Select(l => l.Id).ToList();
            var itemEntities = context.Items.ToList(); // Tải lại danh sách vật tư từ DB để theo dõi thay đổi.
            var rand = new Random();

            // 1. Tạo dữ liệu NHẬP KHO (Inbound) -> Tăng tồn kho -> Ghi nhật ký
            for (int i = 0; i < 10; i++) // Tạo 10 phiếu nhập.
            {
                var inbound = new InboundReceipt
                {
                    SupplierId = supplierIds[rand.Next(supplierIds.Count)],
                    UserId = userIds[rand.Next(userIds.Count)],
                    CreatedDate = DateTime.UtcNow.AddDays(-rand.Next(1, 30)), // Giả lập thời gian nhập kho từ 10 đến 30 ngày trước.
                    Notes = "Nhập hàng mẫu tự động"
                };
                context.InboundReceipts.Add(inbound);
                await context.SaveChangesAsync(); // Lưu để lấy ID phiếu nhập.

                // Tạo chi tiết nhập.
                int numberOfDetails = rand.Next(1, 5);
                for (int d = 0; d < numberOfDetails; d++)
                {
                    var item = itemEntities[rand.Next(itemEntities.Count)];
                    var qty = rand.Next(1, 100); // Tạo số lượng nhập kho ngẫu nhiên.
                    
                    var detail = new InboundReceiptDetail
                    {
                        InboundReceiptId = inbound.Id,
                        ItemId = item.Id,
                        LocationId = locationIds[rand.Next(locationIds.Count)],
                        Quantity = qty
                    };
                    context.InboundReceiptDetails.Add(detail);

                    // Cập nhật số lượng tồn kho.
                    item.CurrentStock += qty;

                    // GHI NHẬT KÝ NHẬP KHO
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        ItemId = item.Id,
                        ActionType = "INBOUND",
                        ChangeQuantity = qty,
                        NewStock = item.CurrentStock,
                        ReferenceId = inbound.Id,
                        Timestamp = inbound.CreatedDate
                    });
                }
            }
            await context.SaveChangesAsync();

            // 2. Tạo dữ liệu XUẤT KHO (Outbound) -> Giảm tồn kho -> Ghi nhật ký
            for (int i = 0; i < 10; i++) // Tạo 10 phiếu xuất.
            {
                var outbound = new OutboundReceipt
                {
                    CustomerId = customerIds[rand.Next(customerIds.Count)],
                    UserId = userIds[rand.Next(userIds.Count)],
                    CreatedDate = DateTime.UtcNow.AddDays(-rand.Next(1, 9)), // Giả lập thời gian xuất kho gần đây.
                    Notes = "Xuất hàng mẫu tự động"
                };
                context.OutboundReceipts.Add(outbound);
                await context.SaveChangesAsync();

                int numberOfDetails = rand.Next(1, 3);
                for (int d = 0; d < numberOfDetails; d++)
                {
                    // Chỉ chọn các vật tư còn tồn kho để thực hiện xuất kho.
                    var availableItems = itemEntities.Where(x => x.CurrentStock > 0).ToList();
                    if (!availableItems.Any()) continue;

                    var item = availableItems[rand.Next(availableItems.Count)];
                    var qty = rand.Next(1, (int)item.CurrentStock / 2); // Đảm bảo số lượng xuất không vượt quá số lượng tồn kho.

                    var detail = new OutboundReceiptDetail
                    {
                        OutboundReceiptId = outbound.Id,
                        ItemId = item.Id,
                        LocationId = locationIds[rand.Next(locationIds.Count)], // Chọn ngẫu nhiên một vị trí kho.
                        Quantity = qty
                    };
                    context.OutboundReceiptDetails.Add(detail);

                    // Cập nhật số lượng tồn kho.
                    item.CurrentStock -= qty;

                    // GHI NHẬT KÝ XUẤT KHO
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        ItemId = item.Id,
                        ActionType = "OUTBOUND",
                        ChangeQuantity = -qty, // Sử dụng giá trị âm cho xuất kho.
                        NewStock = item.CurrentStock,
                        ReferenceId = outbound.Id,
                        Timestamp = outbound.CreatedDate
                    });
                }
            }

            // Lưu toàn bộ các thay đổi vào cơ sở dữ liệu.
            await context.SaveChangesAsync();
        }
    }
}