using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WMS_Demo.Data;
using WMS_Demo.Helpers;
using WMS_Demo.Models;
using WMS_Demo.ViewModels;

namespace WMS_Demo.Controllers
{
    [Authorize]
    public class WarehouseController : Controller
    {
        private readonly WmsDbContext _context;
        private const int PageSize = 10;

        // Định nghĩa action log để quản lý tập trung
        private const string ACTION_INBOUND = "INBOUND";
        private const string ACTION_INBOUND_REV = "INBOUND_REVERT";
        private const string ACTION_OUTBOUND = "OUTBOUND";
        private const string ACTION_OUTBOUND_REV = "OUTBOUND_REVERT";

        public WarehouseController(WmsDbContext context)
        {
            _context = context;
        }

        // ==========================================
        #region NHẬP KHO (INBOUND)
        // ==========================================

        public async Task<IActionResult> InboundIndex(string searchString, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;

            // Bắt đầu câu truy vấn với Include để tải dữ liệu liên quan
            var query = _context.InboundReceipts
                .Include(x => x.Supplier)
                .Include(x => x.CreatedBy)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchLower = searchString.ToLower();
                // Tìm kiếm trên các trường liên quan
                query = query.Where(s => s.Id.ToString().Contains(searchLower) ||
                                         s.Supplier.Name.ToLower().Contains(searchLower) ||
                                         s.CreatedBy.UserName.ToLower().Contains(searchLower));
            }

            var inboundViewModel = query.OrderByDescending(i => i.CreatedDate)
                .Select(x => new ReceiptIndexViewModel
                {
                    Id = x.Id,
                    CreatedDate = x.CreatedDate,
                    ReferenceCode = "PN-" + x.Id, // Format mã phiếu nhập
                    PartnerName = x.Supplier.Name,
                    CreatedBy = x.CreatedBy.UserName,
                    Notes = x.Notes
                });
            return View(await PaginatedList<ReceiptIndexViewModel>.CreateAsync(inboundViewModel, pageNumber ?? 1, PageSize));
        }

        [HttpGet]
        public IActionResult CreateInbound()
        {
            var model = new ReceiptCreateViewModel { Date = DateTime.Now };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInbound(ReceiptCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Tạo phiếu nhập kho thất bại!";
                return View(model);
            }

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var receipt = new InboundReceipt
                {
                     CreatedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 
                                             DateTime.Now.Hour, DateTime.Now.Minute, 0),
                    SupplierId = model.PartnerId,
                    Notes = model.Notes,
                    UserId = userId
                };

                _context.InboundReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                if (model.Details != null && model.Details.Any())
                {
                    foreach (var itemDetail in model.Details)
                    {
                        if (itemDetail.Quantity <= 0) continue;

                        var detail = new InboundReceiptDetail
                        {
                            InboundReceiptId = receipt.Id,
                            ItemId = itemDetail.ItemId,
                            Quantity = itemDetail.Quantity,
                            LocationId = itemDetail.LocationId,
                            UnitPrice = itemDetail.Price
                        };
                        _context.InboundReceiptDetails.Add(detail);

                        var item = await _context.Items.FindAsync(itemDetail.ItemId);
                        if (item != null)
                        {
                            // Cộng tồn kho
                            // Tính toán giá vốn mới
                            decimal currentTotalValue = item.CurrentCost * item.CurrentStock;
                            decimal newInboundValue = itemDetail.Quantity * itemDetail.Price;
                            decimal newTotalStock = item.CurrentStock + itemDetail.Quantity;

                            // Cập nhật giá vốn (Làm tròn 2 số lẻ)
                            // Formula: (Giá cũ * Tồn cũ + Giá nhập * SL nhập) / Tổng tồn mới
                            if (newTotalStock > 0)
                            {
                                item.CurrentCost = Math.Round((currentTotalValue + newInboundValue) / newTotalStock, 2);
                            }

                            item.CurrentStock = newTotalStock;
                            _context.Items.Update(item);

                            // Ghi Log đầy đủ
                            _context.InventoryLogs.Add(new InventoryLog
                            {
                                ItemId = item.Id,
                                ActionType = ACTION_INBOUND,
                                ReferenceId = receipt.Id,
                                ChangeQuantity = itemDetail.Quantity,
                                NewStock = item.CurrentStock,
                                Timestamp = DateTime.Now,
                                TransactionPrice = itemDetail.Price // Lưu giá vốn tại thời điểm nhập
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction(nameof(InboundIndex));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Log error ra file hoặc console để debug
                ModelState.AddModelError("", "Lỗi hệ thống (Transaction Rollbacked): " + ex.Message);
                return View(model);
            }
        }

        public async Task<IActionResult> InboundDetails(int? id)
        {
            if (id == null) return NotFound();

            var receipt = await _context.InboundReceipts
                .Include(r => r.Supplier)
                .Include(r => r.CreatedBy)
                .Include(r => r.Details).ThenInclude(d => d.Item)
                .Include(r => r.Details).ThenInclude(d => d.Location)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (receipt == null) return NotFound();

            return View(receipt);
        }

        // GET: Delete Inbound - Chỉ hiển thị form xác nhận
        public async Task<IActionResult> DeleteInbound(int? id)
        {
            if (id == null) return NotFound();
            var receipt = await _context.InboundReceipts
                .Include(r => r.Supplier)
                .Include(r => r.CreatedBy)
                .Include(r => r.Details) // Cần load details để hiển thị cảnh báo nếu cần
                .FirstOrDefaultAsync(m => m.Id == id);

            if (receipt == null) return NotFound();
            return View(receipt);
        }

        [HttpPost, ActionName("DeleteInbound")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteInboundConfirmed(int id)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Eager loading Details và Item để check tồn kho
                var receipt = await _context.InboundReceipts
                    .Include(r => r.Details)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (receipt == null)
                {
                    TempData["Error"] = "Không tìm thấy phiếu để xóa!";
                    return RedirectToAction(nameof(InboundIndex));
                }

                // Check tồn kho trước khi xóa
                foreach (var d in receipt.Details)
                {
                    var itemCheck = await _context.Items.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == d.ItemId);

                    if (itemCheck == null) continue;

                    if (itemCheck.CurrentStock < d.Quantity)
                    {
                        // Hàng đã bị xuất đi rồi -> CẤM XÓA PHIẾU NHẬP

                        await transaction.RollbackAsync();
                        TempData["Error"] = $"CẢNH BÁO: Sản phẩm {itemCheck.Code} hiện chỉ còn tồn {itemCheck.CurrentStock}, không đủ để hoàn tác {d.Quantity}. Vui lòng kiểm tra lại!";
                        return RedirectToAction(nameof(InboundIndex));
                    }
                }

                // Nếu check OK hết thì mới bắt đầu trừ kho thật
                foreach (var d in receipt.Details)
                {
                    var item = await _context.Items.FindAsync(d.ItemId);
                    if (item != null)
                    {
                        decimal currentTotalValue = item.CurrentCost * item.CurrentStock;
                        decimal valueToRevert = d.Quantity * d.UnitPrice;
                        decimal newStock = item.CurrentStock - d.Quantity;

                        if (newStock > 0)
                        {
                            // Tính lại giá vốn sau khi loại bỏ phiếu nhập này
                            // Có thể ra số ÂM nếu dữ liệu lịch sử bị sai lệch nhiều, nhưng về mặt toán học là đúng hành vi
                            item.CurrentCost = Math.Round((currentTotalValue - valueToRevert) / newStock, 2);
                        }
                        else
                        {
                            // Nếu kho về 0, reset giá vốn về 0 cho đẹp đội hình
                            item.CurrentCost = 1;
                        }

                        item.CurrentStock = newStock;
                        _context.Items.Update(item);

                        // Ghi Log hoàn tác (Revert)
                        _context.InventoryLogs.Add(new InventoryLog
                        {
                            ItemId = item.Id,
                            ActionType = ACTION_INBOUND_REV,
                            ReferenceId = receipt.Id,
                            ChangeQuantity = -d.Quantity, // Số âm thể hiện giảm kho
                            NewStock = item.CurrentStock,
                            Timestamp = DateTime.Now,
                            TransactionPrice = d.UnitPrice
                        });
                    }
                }

                _context.InboundReceipts.Remove(receipt);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Đã xóa phiếu nhập và hoàn tác tồn kho thành công.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi ngoại lệ khi xóa: " + ex.Message;
            }
            return RedirectToAction(nameof(InboundIndex));
        }
        #endregion
        // ==========================================
        #region  XUẤT KHO (OUTBOUND)
        // ==========================================

        public async Task<IActionResult> OutboundIndex(string searchString, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;

            var query = _context.OutboundReceipts
                .Include(x => x.Customer)
                .Include(x => x.CreatedBy)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchLower = searchString.ToLower();
                query = query.Where(s => s.Id.ToString().Contains(searchLower) ||
                                         s.Customer.Name.ToLower().Contains(searchLower) ||
                                         s.CreatedBy.UserName.ToLower().Contains(searchLower));
            }

            var outboundViewModel = query.OrderByDescending(i => i.CreatedDate)
                .Select(x => new ReceiptIndexViewModel
                {
                    Id = x.Id,
                    CreatedDate = x.CreatedDate,
                    ReferenceCode = "PX-" + x.Id, // Format mã phiếu xuất
                    PartnerName = x.Customer.Name,
                    CreatedBy = x.CreatedBy.UserName,
                    Notes = x.Notes
                });

            return View("OutboundIndex", await PaginatedList<ReceiptIndexViewModel>.CreateAsync(outboundViewModel, pageNumber ?? 1, PageSize));
        }

        [HttpGet]
        public IActionResult CreateOutbound()
        {
            var model = new ReceiptCreateViewModel { Date = DateTime.Now };
            return View("CreateOutbound", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOutbound(ReceiptCreateViewModel model)
        {
            // 1. Validation cơ bản
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại!";
                return View("CreateOutbound", model);
            }

            if (model.Details == null || !model.Details.Any())
            {
                TempData["Error"] = "Phiếu xuất kho phải có ít nhất một sản phẩm.";
                return View("CreateOutbound", model);
            }

            // 2. Kiểm tra sơ bộ (Pre-check) trước khi mở Transaction để đỡ tốn tài nguyên DB
            foreach (var itemDetail in model.Details)
            {
                if (itemDetail.Quantity <= 0)
                {
                    TempData["Error"] = $"Số lượng xuất của sản phẩm {itemDetail.ItemCode} phải lớn hơn 0.";
                    return View("CreateOutbound", model);
                }
            }

            // 3. Mở Transaction 
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Tạo Header phiếu xuất
                var receipt = new OutboundReceipt
                {
                    // Luôn lấy thời gian hiện tại của server và làm tròn đến phút
                    // để khớp với định dạng yyyy-MM-ddTHH:mm
                    CreatedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 
                                             DateTime.Now.Hour, DateTime.Now.Minute, 0),
                    CustomerId = model.PartnerId,
                    Notes = model.Notes,
                    UserId = userId
                };

                _context.OutboundReceipts.Add(receipt);
                await _context.SaveChangesAsync(); // Save để lấy ID phiếu

                // Xử lý từng chi tiết (Details)
                foreach (var itemDetail in model.Details)
                {
                    
                    var item = await _context.Items.FindAsync(itemDetail.ItemId);

                    if (item == null)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Sản phẩm ID {itemDetail.ItemId} không tồn tại.";
                        return View("CreateOutbound", model);
                    }

                    // CHECK 1: Tồn kho tổng (Global Stock)
                    if (item.CurrentStock < itemDetail.Quantity)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Lỗi: Sản phẩm {item.Code} - Tổng tồn kho không đủ. (Hiện có: {item.CurrentStock}, Cần: {itemDetail.Quantity})";
                        return View("CreateOutbound", model);
                    }

                    // CHECK 2: Tồn kho tại Vị Trí (Location Stock) - CỰC KỲ QUAN TRỌNG
                    decimal stockAtLocation = await GetStockAtLocationAsync(itemDetail.ItemId, itemDetail.LocationId);

                    if (stockAtLocation < itemDetail.Quantity)
                    {
                        await transaction.RollbackAsync();
                        // Lấy tên vị trí để báo lỗi cho user
                        var locCode = await _context.Locations
                            .Where(l => l.Id == itemDetail.LocationId)
                            .Select(l => l.Code)
                            .FirstOrDefaultAsync() ?? "Unknown";

                        TempData["Error"] = $"Lỗi: Sản phẩm {item.Code} tại vị trí {locCode} chỉ còn {stockAtLocation}, không đủ để xuất {itemDetail.Quantity}.";
                        return View("CreateOutbound", model);
                    }

                    var detail = new OutboundReceiptDetail
                    {
                        OutboundReceiptId = receipt.Id,
                        ItemId = itemDetail.ItemId,
                        Quantity = itemDetail.Quantity,
                        LocationId = itemDetail.LocationId,
                        CostPrice = item.CurrentCost, // Giá vốn xuất
                        SalesPrice = itemDetail.Price // Giá bán
                    };
                    _context.OutboundReceiptDetails.Add(detail);

                    // TRỪ tồn kho tổng
                    item.CurrentStock -= itemDetail.Quantity;
                    _context.Items.Update(item);

                    // Ghi Log xuất kho
                    _context.InventoryLogs.Add(new InventoryLog
                    {
                        ItemId = item.Id,
                        ActionType = ACTION_OUTBOUND,
                        ReferenceId = receipt.Id,
                        ChangeQuantity = -itemDetail.Quantity,
                        NewStock = item.CurrentStock,
                        Timestamp = DateTime.Now,
                        TransactionPrice = item.CurrentCost
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Tạo phiếu xuất kho thành công! (Đã kiểm tra kỹ tồn kho theo vị trí)";
                return RedirectToAction(nameof(OutboundIndex));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Log ex ra file log thật nếu có
                ModelState.AddModelError("", "Lỗi hệ thống (Transaction Rollbacked): " + ex.Message);
                return View("CreateOutbound", model);
            }
        }

        public async Task<IActionResult> OutboundDetails(int? id)
        {
            if (id == null) return NotFound();

            var receipt = await _context.OutboundReceipts
                .Include(r => r.Customer)
                .Include(r => r.CreatedBy)
                .Include(r => r.Details).ThenInclude(d => d.Item)
                .Include(r => r.Details).ThenInclude(d => d.Location)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (receipt == null) return NotFound();

            return View("OutboundDetails", receipt);
        }


        public async Task<IActionResult> DeleteOutbound(int? id)
        {
            if (id == null) return NotFound();
            var receipt = await _context.OutboundReceipts
                .Include(r => r.Customer)
                .Include(r => r.CreatedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (receipt == null) return NotFound();

            return View("DeleteOutbound", receipt);
        }


        [HttpPost, ActionName("DeleteOutbound")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOutboundConfirmed(int id)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var receipt = await _context.OutboundReceipts
                    .Include(r => r.Details)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (receipt == null)
                {
                    TempData["Error"] = "Không tìm thấy phiếu để xóa!";
                    return RedirectToAction(nameof(OutboundIndex));
                }

                // Hoàn tác tồn kho
                foreach (var d in receipt.Details)
                {
                    var item = await _context.Items.FindAsync(d.ItemId);
                    if (item != null)
                    {
                        // Hoàn tác (cộng lại) tồn kho
                        item.CurrentStock += d.Quantity;
                        _context.Items.Update(item);

                        // Ghi Log hoàn tác (Revert)
                        _context.InventoryLogs.Add(new InventoryLog
                        {
                            ItemId = item.Id,
                            ActionType = ACTION_OUTBOUND_REV,
                            ReferenceId = receipt.Id,
                            ChangeQuantity = d.Quantity, // Số dương thể hiện tăng kho
                            NewStock = item.CurrentStock,
                            Timestamp = DateTime.Now,
                            TransactionPrice = d.CostPrice // Giá tại thời điểm xuất ban đầu
                        });
                    }
                }

                _context.OutboundReceipts.Remove(receipt);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Đã xóa phiếu xuất và hoàn tác tồn kho thành công.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi ngoại lệ khi xóa: " + ex.Message;
            }

            return RedirectToAction(nameof(OutboundIndex));
        }

        #endregion
        // ==========================================
        #region  KHU VỰC: AJAX API (Phục vụ Search Dropdown Select2)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> SearchItems(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new List<object>());

            var items = await _context.Items.AsNoTracking()
                .Where(i => i.Name.Contains(term) || i.Code.Contains(term))
                .Select(i => new
                {
                    id = i.Id,
                    text = $"{i.Code} - {i.Name} (Tồn: {i.CurrentStock})",
                    unit = i.Unit,
                    price = 0
                })
                .Take(20)
                .ToListAsync();

            return Json(items);
        }

        [HttpGet]
        public async Task<IActionResult> SearchSuppliers(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new List<object>());

            var data = await _context.Suppliers.AsNoTracking()
                .Where(s => s.Name.Contains(term) || s.PhoneNumber.Contains(term))
                .Select(s => new { id = s.Id, text = s.Name })
                .Take(20)
                .ToListAsync();
            return Json(data);
        }
        [HttpGet]
        public async Task<IActionResult> SearchCustomers(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new List<object>());

            var data = await _context.Customers.AsNoTracking()
                .Where(s => s.Name.Contains(term) || s.PhoneNumber.Contains(term))
                .Select(s => new { id = s.Id, text = s.Name })
                .Take(20)
                .ToListAsync();
            return Json(data);
        }
        [HttpGet]
        public async Task<IActionResult> SearchLocations(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new List<object>());

            var data = await _context.Locations.AsNoTracking()
                .Where(s => s.Code.Contains(term))
                .Select(s => new { id = s.Id, text = s.Code })
                .Take(20)
                .ToListAsync();
            return Json(data);
        }
        //TODO LIST: TẠO BẢNG NỐI ITEM-LOCATION ĐỂ QUẢN LÝ TỒN THEO VỊ TRÍ BỎ CODe DƯỚI
        public async Task<IActionResult> GetLocationsForItem(int itemId)
        {
            // 1. Lấy tất cả phiếu nhập của Item này, group theo Location
            var inbound = await _context.InboundReceiptDetails
                .Where(x => x.ItemId == itemId)
                .GroupBy(x => x.LocationId)
                .Select(g => new { LocationId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToListAsync();

            // 2. Lấy tất cả phiếu xuất của Item này, group theo Location
            var outbound = await _context.OutboundReceiptDetails
                .Where(x => x.ItemId == itemId)
                .GroupBy(x => x.LocationId)
                .Select(g => new { LocationId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToListAsync();

            // 3. Join lại để tính tồn kho thực tế (Tồn = Nhập - Xuất)
            var locationStocks = from i in inbound
                                 join o in outbound on i.LocationId equals o.LocationId into joined
                                 from o in joined.DefaultIfEmpty()
                                 let outQty = o?.Qty ?? 0
                                 let stock = i.Qty - outQty
                                 where stock > 0 // Chỉ lấy vị trí còn hàng
                                 select new { i.LocationId, Stock = stock };

            // 4. Lấy thêm thông tin tên/code của Location để hiển thị
            var locationIds = locationStocks.Select(x => x.LocationId).ToList();
            var locations = await _context.Locations
                .Where(x => locationIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Code); // Dictionary cho nhanh

            var result = locationStocks.Select(x => new
            {
                id = x.LocationId,
                text = $"{locations[x.LocationId]} (Sẵn: {x.Stock})", // Hiển thị: Kệ A-01 (Sẵn: 50)
                stock = x.Stock
            }).ToList();

            return Json(result);
        }
        private async Task<decimal> GetStockAtLocationAsync(int itemId, int locationId)
        {
            // 1. Tổng nhập vào vị trí này
            var totalInbound = await _context.InboundReceiptDetails
                .Where(x => x.ItemId == itemId && x.LocationId == locationId)
                .SumAsync(x => x.Quantity);

            // 2. Tổng xuất từ vị trí này
            var totalOutbound = await _context.OutboundReceiptDetails
                .Where(x => x.ItemId == itemId && x.LocationId == locationId)
                .SumAsync(x => x.Quantity);

            // Tồn hiện tại = Nhập - Xuất
            return totalInbound - totalOutbound;
        }
        #endregion
    }
}