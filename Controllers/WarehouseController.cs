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

        public WarehouseController(WmsDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // NHẬP KHO (INBOUND)
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
                    CreatedDate = model.Date,
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

        // ==========================================
        // KHU VỰC: AJAX API (Phục vụ Search Dropdown Select2)
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
    }
}