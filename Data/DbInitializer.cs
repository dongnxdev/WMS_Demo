using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Models;

namespace WMS_Demo.Data
{
    public static class DbInitializer
    {
        // Phương thức khởi tạo dữ liệu mẫu cho cơ sở dữ liệu.
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<WmsDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Đảm bảo cơ sở dữ liệu đã được tạo.
            context.Database.EnsureCreated();

            // Kiểm tra nếu đã có dữ liệu (dựa vào bảng Users) thì không thực hiện seed.
            if (await userManager.Users.AnyAsync())
            {
                Console.WriteLine("Database đã có dữ liệu. Bỏ qua quá trình khởi tạo dữ liệu mẫu.");
                return;
            }

            // --- Khởi tạo Vai trò (Roles) ---
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // --- 2. Khởi tạo Người dùng (Users) và gán vai trò ---
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
            await userManager.AddToRoleAsync(adminUser, "Admin"); // Gán vai trò "Admin".

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
            await userManager.AddToRoleAsync(staffUser, "User"); // Gán vai trò "User".

            var userFaker = new Faker<ApplicationUser>()
                .RuleFor(u => u.FullName, f => f.Name.FullName())
                .RuleFor(u => u.Email, f => f.Internet.Email(provider: "wms.com"))
                .RuleFor(u => u.UserName, (f, u) => u.Email) // Mặc định UserName giống Email.
                .RuleFor(u => u.StaffCode, f => $"STAFF-{f.Random.Number(100, 999)}")
                .RuleFor(u => u.IsActive, true)
                .RuleFor(u => u.EmailConfirmed, true);

            var fakeUsers = userFaker.Generate(5); // Tạo 5 người dùng mẫu khác.
            foreach (var user in fakeUsers)
            {
                await userManager.CreateAsync(user, "Abc@12345"); // Tạo người dùng.
                await userManager.AddToRoleAsync(user, "User");   // Gán vai trò "User".
            }


            // Lấy danh sách ID người dùng để sử dụng trong các giao dịch.
            var userIds = fakeUsers.Select(u => u.Id).ToList();

            // --- 3. Khởi tạo Dữ liệu gốc (Master Data) ---

            // Tạo dữ liệu mẫu cho Nhà cung cấp.
            var supplierFaker = new Faker<Supplier>()
                .RuleFor(s => s.Name, f => f.Company.CompanyName())
                .RuleFor(s => s.Address, f => f.Address.FullAddress())
                .RuleFor(s => s.PhoneNumber, f => f.Phone.PhoneNumber("09########"));
            var suppliers = supplierFaker.Generate(10);
            context.Suppliers.AddRange(suppliers);

            // Tạo dữ liệu mẫu cho Khách hàng.
            var customerFaker = new Faker<Customer>()
                .RuleFor(c => c.Name, f => f.Name.FullName())
                .RuleFor(c => c.Address, f => f.Address.FullAddress())
                .RuleFor(c => c.PhoneNumber, f => f.Phone.PhoneNumber("09########"));
            var customers = customerFaker.Generate(10);
            context.Customers.AddRange(customers);

            // Tạo dữ liệu mẫu cho Vị trí trong kho.
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

            // Tạo dữ liệu mẫu cho Vật tư. Tồn kho ban đầu là 0.
            var rand = new Random();
            var itemFaker = new Faker<Item>()
                .RuleFor(i => i.Name, f => f.Commerce.ProductName())
                .RuleFor(i => i.Code, f => f.Commerce.Ean8())
                .RuleFor(i => i.Unit, f => f.PickRandom("Cái", "Hộp", "Kg"))
                .RuleFor(i => i.SafetyStock, f => rand.Next(1, 100))
                .RuleFor(i => i.CurrentStock, 0); // (Tồn kho sẽ được cập nhật thông qua phiếu nhập kho).
            var items = itemFaker.Generate(20);
            context.Items.AddRange(items);

            await context.SaveChangesAsync(); // Lưu các dữ liệu gốc để lấy ID cho các bước sau.

            // --- 4. Khởi tạo Giao dịch (Transactions) ---

            // Lấy danh sách ID của các đối tượng vừa tạo.
            var supplierIds = suppliers.Select(s => s.Id).ToList();
            var customerIds = customers.Select(c => c.Id).ToList();
            var locationIds = locations.Select(l => l.Id).ToList();
            var itemEntities = context.Items.ToList(); // Lấy lại danh sách vật tư đã được context theo dõi.


            // Tạo các phiếu nhập kho mẫu.
            for (int i = 0; i < 10; i++) // Tạo 10 phiếu nhập ngẫu nhiên.
            {
                var inbound = new InboundReceipt
                {
                    SupplierId = supplierIds[rand.Next(supplierIds.Count)],
                    UserId = userIds[rand.Next(userIds.Count)],
                    CreatedDate = DateTime.UtcNow.AddDays(-rand.Next(1, 30)), // Ngày nhập ngẫu nhiên trong 30 ngày qua.
                    Notes = "Nhập hàng demo tự động"
                };
                context.InboundReceipts.Add(inbound);
                await context.SaveChangesAsync(); // Lưu để lấy ID của phiếu nhập.

                // Tạo các chi tiết cho phiếu nhập.
                int numberOfDetails = rand.Next(1, 5);
                for (int d = 0; d < numberOfDetails; d++)
                {
                    var item = itemEntities[rand.Next(itemEntities.Count)];
                    var qty = rand.Next(1, 100); // Số lượng nhập ngẫu nhiên.
                    var costPerUnit = rand.Next(10, 500); // Giá nhập ngẫu nhiên.
                    var detail = new InboundReceiptDetail
                    {
                        InboundReceiptId = inbound.Id,
                        ItemId = item.Id,
                        LocationId = locationIds[rand.Next(locationIds.Count)],
                        Quantity = qty,
                        UnitPrice = costPerUnit
                    };
                    context.InboundReceiptDetails.Add(detail);

                    // Cập nhật tồn kho và giá vốn trung bình cho vật tư.
                    item.CurrentCost = (item.CurrentCost * (item.CurrentStock) + (qty * costPerUnit));
                    item.CurrentCost /= (item.CurrentStock + qty);

                    item.CurrentStock += qty;

                    // Ghi log giao dịch nhập kho.
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        ItemId = item.Id,
                        ActionType = "INBOUND",
                        ChangeQuantity = qty,
                        NewStock = item.CurrentStock,
                        ReferenceId = inbound.Id,
                        Timestamp = inbound.CreatedDate,
                        TransactionPrice = costPerUnit,
                        MovingAverageCost = item.CurrentCost
                    });
                }
            }
            await context.SaveChangesAsync();

            // Tạo các phiếu xuất kho mẫu.
            for (int i = 0; i < 10; i++) // Tạo 10 phiếu xuất ngẫu nhiên.
            {
                var outbound = new OutboundReceipt
                {
                    CustomerId = customerIds[rand.Next(customerIds.Count)],
                    UserId = userIds[rand.Next(userIds.Count)],
                    CreatedDate = DateTime.UtcNow.AddDays(-rand.Next(1, 9)), // Ngày xuất ngẫu nhiên trong 9 ngày qua.
                    Notes = "Xuất hàng demo tự động"
                };
                context.OutboundReceipts.Add(outbound);
                await context.SaveChangesAsync();

                int numberOfDetails = rand.Next(1, 3);
                for (int d = 0; d < numberOfDetails; d++)
                {
                    // Chỉ xuất các vật tư có tồn kho.
                    var availableItems = itemEntities.Where(x => x.CurrentStock > 0).ToList();
                    if (!availableItems.Any()) continue;

                    var item = availableItems[rand.Next(availableItems.Count)];
                    var qty = rand.Next(1, (int)item.CurrentStock / 2); // Số lượng xuất ngẫu nhiên, đảm bảo không âm kho.
                    var salePrice = rand.Next(10, 500);
                    var costPrice = item.CurrentCost;
                    var detail = new OutboundReceiptDetail
                    {
                        OutboundReceiptId = outbound.Id,
                        ItemId = item.Id,
                        LocationId = locationIds[rand.Next(locationIds.Count)], // Lấy ngẫu nhiên từ một vị trí.
                        Quantity = qty,
                        SalesPrice = salePrice,
                        CostPrice = costPrice
                    };
                    context.OutboundReceiptDetails.Add(detail);

                    // Cập nhật lại tồn kho.
                    item.CurrentStock -= qty;

                    // Ghi log giao dịch xuất kho.
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        ItemId = item.Id,
                        ActionType = "OUTBOUND",
                        ChangeQuantity = -qty, // Ghi nhận số lượng giảm.
                        NewStock = item.CurrentStock,
                        ReferenceId = outbound.Id,
                        Timestamp = outbound.CreatedDate,
                        TransactionPrice = salePrice,
                        MovingAverageCost = item.CurrentCost
                    });
                }
            }

            // Lưu tất cả các thay đổi cuối cùng.
            await context.SaveChangesAsync();
        }
    }
}