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

        // Định nghĩa hằng số cho các loại hành động ghi log.
        private const string ACTION_INBOUND = "INBOUND";
        private const string ACTION_INBOUND_REV = "INBOUND_REVERT";
        private const string ACTION_OUTBOUND = "OUTBOUND";
        private const string ACTION_OUTBOUND_REV = "OUTBOUND_REVERT";

        public WarehouseController(WmsDbContext context)
        {
            _context = context;
        }

        // ==========================================
        #region Nhập kho (Inbound)
        // ==========================================

        public async Task<IActionResult> InboundIndex(string searchString, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;

            // Truy vấn phiếu nhập, tải kèm thông tin Nhà cung cấp và Người tạo.
            var query = _context.InboundReceipts
                .Include(x => x.Supplier)
                .Include(x => x.CreatedBy)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchLower = searchString.ToLower();
                // Lọc theo mã phiếu, tên nhà cung cấp, hoặc tên người tạo.
                query = query.Where(s => s.Id.ToString().Contains(searchLower) ||
                                         s.Supplier.Name.ToLower().Contains(searchLower) ||
                                         s.CreatedBy.UserName.ToLower().Contains(searchLower));
            }

            var inboundViewModel = query.OrderByDescending(i => i.CreatedDate)
                .Select(x => new ReceiptIndexViewModel
                {
                    Id = x.Id,
                    CreatedDate = x.CreatedDate,
                    ReferenceCode = "PN-" + x.Id, // Định dạng mã phiếu nhập.
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
                            // Cập nhật tồn kho và tính lại giá vốn trung bình.
                            decimal currentTotalValue = item.CurrentCost * item.CurrentStock;
                            decimal newInboundValue = itemDetail.Quantity * itemDetail.Price;
                            decimal newTotalStock = item.CurrentStock + itemDetail.Quantity;

                            // Công thức: (Giá trị tồn hiện tại + Giá trị nhập) / Tổng tồn mới.
                            if (newTotalStock > 0)
                            {
                                item.CurrentCost = Math.Round((currentTotalValue + newInboundValue) / newTotalStock, 2);
                            }

                            item.CurrentStock = newTotalStock;
                            _context.Items.Update(item);

                            // Ghi log nghiệp vụ nhập kho.
                            _context.InventoryLogs.Add(new InventoryLog
                            {
                                ItemId = item.Id,
                                ActionType = ACTION_INBOUND,
                                ReferenceId = receipt.Id,
                                ChangeQuantity = itemDetail.Quantity,
                                NewStock = item.CurrentStock,
                                Timestamp = DateTime.Now,
                                TransactionPrice = itemDetail.Price // Lưu giá tại thời điểm giao dịch.
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
                // Ghi log lỗi và rollback giao dịch.
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

        // GET: Hiển thị form xác nhận xóa phiếu nhập.
        public async Task<IActionResult> DeleteInbound(int? id)
        {
            if (id == null) return NotFound();
            var receipt = await _context.InboundReceipts
                .Include(r => r.Supplier)
                .Include(r => r.CreatedBy)
                .Include(r => r.Details) // Tải chi tiết phiếu để kiểm tra.
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
                // Tải chi tiết phiếu để xử lý hoàn kho.
                var receipt = await _context.InboundReceipts
                    .Include(r => r.Details)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (receipt == null)
                {
                    TempData["Error"] = "Không tìm thấy phiếu để xóa!";
                    return RedirectToAction(nameof(InboundIndex));
                }

                // Kiểm tra điều kiện hoàn kho.
                foreach (var d in receipt.Details)
                {
                    var itemCheck = await _context.Items.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == d.ItemId);

                    if (itemCheck == null) continue;

                    // Không cho phép xóa phiếu nhập nếu số lượng tồn kho không đủ để hoàn tác.
                    if (itemCheck.CurrentStock < d.Quantity)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"CẢNH BÁO: Sản phẩm {itemCheck.Code} hiện chỉ còn tồn {itemCheck.CurrentStock}, không đủ để hoàn tác {d.Quantity}. Vui lòng kiểm tra lại!";
                        return RedirectToAction(nameof(InboundIndex));
                    }
                }

                // Hoàn tác số lượng và tính lại giá vốn cho từng sản phẩm.
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
                            // Tính lại giá vốn sau khi hoàn tác.
                            // Lưu ý: Giá vốn có thể âm nếu lịch sử dữ liệu không nhất quán.
                            item.CurrentCost = Math.Round((currentTotalValue - valueToRevert) / newStock, 2);
                        }
                        else
                        {
                            // Nếu tồn kho về 0, đặt lại giá vốn.
                            item.CurrentCost = 0;
                        }

                        item.CurrentStock = newStock;
                        _context.Items.Update(item);

                        // Ghi log nghiệp vụ hoàn tác nhập kho.
                        _context.InventoryLogs.Add(new InventoryLog
                        {
                            ItemId = item.Id,
                            ActionType = ACTION_INBOUND_REV,
                            ReferenceId = receipt.Id,
                            ChangeQuantity = -d.Quantity, // Ghi nhận số lượng giảm.
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
        #region Xuất kho (Outbound)
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
                    ReferenceCode = "PX-" + x.Id, // Định dạng mã phiếu xuất.
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
            // 1. Kiểm tra tính hợp lệ của model.
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

            // 2. Kiểm tra sơ bộ dữ liệu đầu vào.
            foreach (var itemDetail in model.Details)
            {
                if (itemDetail.Quantity <= 0)
                {
                    TempData["Error"] = $"Số lượng xuất của sản phẩm {itemDetail.ItemCode} phải lớn hơn 0.";
                    return View("CreateOutbound", model);
                }
            }

            // 3. Bắt đầu giao dịch cơ sở dữ liệu.
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Tạo thông tin chính của phiếu xuất.
                var receipt = new OutboundReceipt
                {
                    // Chuẩn hóa thời gian tạo (tương thích với định dạng input datetime-local).
                    CreatedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                             DateTime.Now.Hour, DateTime.Now.Minute, 0),
                    CustomerId = model.PartnerId,
                    Notes = model.Notes,
                    UserId = userId
                };

                _context.OutboundReceipts.Add(receipt);
                await _context.SaveChangesAsync(); // Lưu để lấy ID cho các chi tiết.

                // Xử lý từng dòng chi tiết của phiếu xuất.
                foreach (var itemDetail in model.Details)
                {
                    var item = await _context.Items.FindAsync(itemDetail.ItemId);

                    if (item == null)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Sản phẩm ID {itemDetail.ItemId} không tồn tại.";
                        return View("CreateOutbound", model);
                    }

                    // Kiểm tra tồn kho tổng của sản phẩm.
                    if (item.CurrentStock < itemDetail.Quantity)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Lỗi: Sản phẩm {item.Code} - Tổng tồn kho không đủ. (Hiện có: {item.CurrentStock}, Cần: {itemDetail.Quantity})";
                        return View("CreateOutbound", model);
                    }

                    // Kiểm tra tồn kho tại vị trí xuất.
                    decimal stockAtLocation = await GetStockAtLocationAsync(itemDetail.ItemId, itemDetail.LocationId);

                    if (stockAtLocation < itemDetail.Quantity)
                    {
                        await transaction.RollbackAsync();
                        // Lấy mã vị trí để hiển thị lỗi.
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
                        CostPrice = item.CurrentCost, // Ghi nhận giá vốn tại thời điểm xuất.
                        SalesPrice = itemDetail.Price // Ghi nhận giá bán.
                    };
                    _context.OutboundReceiptDetails.Add(detail);

                    // Cập nhật lại tồn kho tổng.
                    item.CurrentStock -= itemDetail.Quantity;
                    _context.Items.Update(item);

                    // Ghi log nghiệp vụ xuất kho.
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
                // Ghi log lỗi và rollback giao dịch.
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

                // Bắt đầu quá trình hoàn tác tồn kho.
                foreach (var d in receipt.Details)
                {
                    var item = await _context.Items.FindAsync(d.ItemId);
                    if (item != null)
                    {
                        // Cập nhật lại tồn kho.
                        item.CurrentStock += d.Quantity;
                        _context.Items.Update(item);

                        // Ghi log nghiệp vụ hoàn tác xuất kho.
                        _context.InventoryLogs.Add(new InventoryLog
                        {
                            ItemId = item.Id,
                            ActionType = ACTION_OUTBOUND_REV,
                            ReferenceId = receipt.Id,
                            ChangeQuantity = d.Quantity, // Ghi nhận số lượng tăng.
                            NewStock = item.CurrentStock,
                            Timestamp = DateTime.Now,
                            TransactionPrice = d.CostPrice // Sử dụng lại giá vốn tại thời điểm xuất ban đầu.
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
        #region API (Ajax)
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
        // TODO: Chuyển sang bảng tồn kho nối theo vị trí (ItemLocationStock) để tối ưu hiệu năng.
        public async Task<IActionResult> GetLocationsForItem(int itemId)
        {
            // 1. Tổng hợp số lượng nhập theo từng vị trí.
            var inbound = await _context.InboundReceiptDetails
                .Where(x => x.ItemId == itemId)
                .GroupBy(x => x.LocationId)
                .Select(g => new { LocationId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToListAsync();

            // 2. Tổng hợp số lượng xuất theo từng vị trí.
            var outbound = await _context.OutboundReceiptDetails
                .Where(x => x.ItemId == itemId)
                .GroupBy(x => x.LocationId)
                .Select(g => new { LocationId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToListAsync();

            // 3. Tính tồn kho thực tế tại mỗi vị trí.
            var locationStocks = from i in inbound
                                 join o in outbound on i.LocationId equals o.LocationId into joined
                                 from o in joined.DefaultIfEmpty()
                                 let outQty = o?.Qty ?? 0
                                 let stock = i.Qty - outQty
                                 where stock > 0 // Lọc các vị trí có tồn kho.
                                 select new { i.LocationId, Stock = stock };

            // 4. Lấy thông tin chi tiết của các vị trí hợp lệ.
            var locationIds = locationStocks.Select(x => x.LocationId).ToList();
            var locations = await _context.Locations
                .Where(x => locationIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Code); // Dùng Dictionary để tra cứu nhanh.

            var result = locationStocks.Select(x => new
            {
                id = x.LocationId,
                text = $"{locations[x.LocationId]} (Sẵn: {x.Stock})", // Định dạng hiển thị cho dropdown.
                stock = x.Stock
            }).ToList();

            return Json(result);
        }
        private async Task<decimal> GetStockAtLocationAsync(int itemId, int locationId)
        {
            // Tính tổng số lượng đã nhập vào vị trí.
            var totalInbound = await _context.InboundReceiptDetails
                .Where(x => x.ItemId == itemId && x.LocationId == locationId)
                .SumAsync(x => x.Quantity);

            // Tính tổng số lượng đã xuất từ vị trí.
            var totalOutbound = await _context.OutboundReceiptDetails
                .Where(x => x.ItemId == itemId && x.LocationId == locationId)
                .SumAsync(x => x.Quantity);

            // Tồn kho hiện tại = Tổng nhập - Tổng xuất.
            return totalInbound - totalOutbound;
        }
        #endregion
    }
}