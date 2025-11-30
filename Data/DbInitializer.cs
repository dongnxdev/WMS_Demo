using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Models;

namespace WMS_Demo.Data
{
    public static class DbInitializer
    {
        // Chuyển sang Async để xử lý User
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<WmsDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Tạo Database nếu chưa có
            context.Database.EnsureCreated();

            // Check User rồi thì coi như đã seed, 
            if (await userManager.Users.AnyAsync())
            {
                Console.WriteLine("Database đã tồn tại");
                return; 
            }

            // --- A. TẠO ACCOUNT (USER) ---
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
        
            
            // Lấy ID của tất cả người dùng để lát gán vào phiếu nhập/xuất
            var userIds = fakeUsers.Select(u => u.Id).ToList();

            // --- B. TẠO MASTER DATA (NHÀ CC, KHÁCH, KHO, VẬT TƯ) ---
            
            // 1. Suppliers
            var supplierFaker = new Faker<Supplier>()
                .RuleFor(s => s.Name, f => f.Company.CompanyName())
                .RuleFor(s => s.Address, f => f.Address.FullAddress())
                .RuleFor(s => s.PhoneNumber, f => f.Phone.PhoneNumber("09########"));
            var suppliers = supplierFaker.Generate(10);
            context.Suppliers.AddRange(suppliers);

            // 2. Customers
            var customerFaker = new Faker<Customer>()
                .RuleFor(c => c.Name, f => f.Name.FullName())
                .RuleFor(c => c.Address, f => f.Address.FullAddress())
                .RuleFor(c => c.PhoneNumber, f => f.Phone.PhoneNumber("09########"));
            var customers = customerFaker.Generate(10);
            context.Customers.AddRange(customers);

            // 3. Locations
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

            // 4. Items (Lưu ý: Để tồn kho = 0 ban đầu, ta sẽ tăng nó qua phiếu Nhập kho để có Log chuẩn)
            var rand = new Random();
            var itemFaker = new Faker<Item>()
                .RuleFor(i => i.Name, f => f.Commerce.ProductName())
                .RuleFor(i => i.Code, f => f.Commerce.Ean8())
                .RuleFor(i => i.Unit, f => f.PickRandom("Cái", "Hộp", "Kg"))
                .RuleFor(i => i.SafetyStock, f => rand.Next(1, 100))
                .RuleFor(i => i.CurrentStock, 0); // Bắt đầu bằng 0
            var items = itemFaker.Generate(20);
            context.Items.AddRange(items);

            await context.SaveChangesAsync(); // Lưu đợt 1 để có ID dùng cho các bảng quan hệ

            // --- C. TẠO TRANSACTION (NHẬP/XUẤT & LOGS) ---
            
            // Chúng ta cần danh sách ID đã lưu để random
            var supplierIds = suppliers.Select(s => s.Id).ToList();
            var customerIds = customers.Select(c => c.Id).ToList();
            var locationIds = locations.Select(l => l.Id).ToList();
            var itemEntities = context.Items.ToList(); // Lấy lại items từ DB để track change
           

            // 1. Tạo dữ liệu NHẬP KHO (Inbound) -> Tăng tồn kho -> Ghi Log
            for (int i = 0; i < 10; i++) // Tạo 10 phiếu nhập
            {
                var inbound = new InboundReceipt
                {
                    SupplierId = supplierIds[rand.Next(supplierIds.Count)],
                    UserId = userIds[rand.Next(userIds.Count)],
                    CreatedDate = DateTime.UtcNow.AddDays(-rand.Next(1, 30)), // Nhập cách đây 10-30 ngày
                    Notes = "Nhập hàng demo tự động"
                };
                context.InboundReceipts.Add(inbound);
                await context.SaveChangesAsync(); // Lưu để lấy Inbound ID

                // Tạo chi tiết nhập
                int numberOfDetails = rand.Next(1, 5);
                for (int d = 0; d < numberOfDetails; d++)
                {
                    var item = itemEntities[rand.Next(itemEntities.Count)];
                    var qty = rand.Next(1, 100); // Nhập số lượng lớn
                    var costPerUnit = rand.Next(10, 500); // Giá vốn nhập
                    var detail = new InboundReceiptDetail
                    {
                        InboundReceiptId = inbound.Id,
                        ItemId = item.Id,
                        LocationId = locationIds[rand.Next(locationIds.Count)],
                        Quantity = qty,
                        UnitPrice=costPerUnit
                    };
                    context.InboundReceiptDetails.Add(detail);

                    // Cập nhật tồn kho
                    item.CurrentCost= (item.CurrentCost* (item.CurrentStock)+(qty*costPerUnit));
                    item.CurrentCost/= (item.CurrentStock + qty);
                    
                    item.CurrentStock += qty;
                    
                    // GHI LOG INBOUND
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        ItemId = item.Id,
                        ActionType = "INBOUND",
                        ChangeQuantity = qty,
                        NewStock = item.CurrentStock,
                        ReferenceId = inbound.Id,
                        Timestamp = inbound.CreatedDate,
                        TransactionPrice= costPerUnit,
                        MovingAverageCost = item.CurrentCost
                    });
                }
            }
            await context.SaveChangesAsync();

            // 2. Tạo dữ liệu XUẤT KHO (Outbound) -> Giảm tồn kho -> Ghi Log
            for (int i = 0; i < 10; i++) // Tạo 10 phiếu xuất
            {
                var outbound = new OutboundReceipt
                {
                    CustomerId = customerIds[rand.Next(customerIds.Count)],
                    UserId = userIds[rand.Next(userIds.Count)],
                    CreatedDate = DateTime.UtcNow.AddDays(-rand.Next(1, 9)), // Xuất gần đây hơn
                    Notes = "Xuất hàng demo tự động"
                };
                context.OutboundReceipts.Add(outbound);
                await context.SaveChangesAsync();

                int numberOfDetails = rand.Next(1, 3);
                for (int d = 0; d < numberOfDetails; d++)
                {
                    // Chỉ chọn item nào còn hàng để xuất
                    var availableItems = itemEntities.Where(x => x.CurrentStock > 0).ToList();
                    if (!availableItems.Any()) continue;

                    var item = availableItems[rand.Next(availableItems.Count)];
                    var qty = rand.Next(1, (int)item.CurrentStock / 2); // Xuất ít thôi, ko âm kho
                    var salePrice = rand.Next(10, 500);
                    var costPrice = item.CurrentCost;
                    var detail = new OutboundReceiptDetail
                    {
                        OutboundReceiptId = outbound.Id,
                        ItemId = item.Id,
                        LocationId = locationIds[rand.Next(locationIds.Count)], // Lấy đại từ vị trí nào đó
                        Quantity = qty,
                        SalesPrice=salePrice,
                        CostPrice=costPrice
                    };
                    context.OutboundReceiptDetails.Add(detail);

                    // Cập nhật tồn kho
                    item.CurrentStock -= qty;

                    // GHI LOG OUTBOUND
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        ItemId = item.Id,
                        ActionType = "OUTBOUND",
                        ChangeQuantity = -qty, // Số âm
                        NewStock = item.CurrentStock,
                        ReferenceId = outbound.Id,
                        Timestamp = outbound.CreatedDate,
                        TransactionPrice= salePrice,
                        MovingAverageCost = item.CurrentCost
                    });
                }
            }

            // Lưu chốt sổ cuối cùng
            await context.SaveChangesAsync();
        }
    }
}