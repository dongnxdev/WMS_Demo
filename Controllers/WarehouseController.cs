using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WMS_Demo.Models;
using WMS_Demo.ViewModels;
using WMS_Demo.Data;

namespace WMS_Demo.Controllers
{
    [Authorize] 
    public class WarehouseController : Controller
    {
        private readonly WmsDbContext _context;

        public WarehouseController(WmsDbContext context)
        {
            _context = context;
        }

        // PHẦN XỬ LÝ NHẬP KHO
        
        [HttpGet]
        public IActionResult CreateInbound()
        {
            // Chuẩn bị dữ liệu cho các dropdown list trên giao diện.
            ViewBag.Suppliers = new SelectList(_context.Suppliers, "Id", "Name");
            ViewBag.Items = _context.Items.ToList(); // Lấy danh sách vật tư để xử lý phía client.
            ViewBag.Locations = _context.Locations.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken] 
        public IActionResult CreateInbound(WarehouseTransactionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Nếu dữ liệu không hợp lệ, tải lại giao diện.
                ViewBag.Suppliers = new SelectList(_context.Suppliers, "Id", "Name");
                return View(model);
            }
           
            // Bắt đầu một transaction để đảm bảo tính toàn vẹn dữ liệu.
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // 1. Tạo phiếu nhập kho chính.
                    var receipt = new InboundReceipt
                    {
                        CreatedDate = model.Date,
                        SupplierId = model.BusinessPartnerId ?? 0, // Xử lý trường hợp `BusinessPartnerId` có thể là null.
                        Notes = model.Remarks,
                        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    };
                    _context.InboundReceipts.Add(receipt);
                    _context.SaveChanges(); // Lưu thay đổi để lấy ID của phiếu nhập kho vừa tạo.

                    // 2. Duyệt qua từng chi tiết của giao dịch.
                    foreach (var detail in model.Details)
                    {
                        // a. Lưu chi tiết phiếu nhập kho.
                        var receiptDetail = new InboundReceiptDetail
                        {
                            InboundReceiptId = receipt.Id,
                            ItemId = detail.ItemId,
                            Quantity = detail.Quantity,
                            LocationId = detail.LocationId
                        };
                        _context.InboundReceiptDetails.Add(receiptDetail);

                        // b. Cập nhật số lượng tồn kho của vật tư.
                        var item = _context.Items.Find(detail.ItemId);
                        if (item != null)
                        {
                            // c. Tăng số lượng tồn kho hiện tại.
                            item.CurrentStock += detail.Quantity;
                            _context.Items.Update(item);

                            // d. Ghi lại lịch sử nhập kho để theo dõi.
                            var log = new InventoryLog
                            {
                                ItemId = detail.ItemId,
                                ActionType = "INBOUND",
                                ChangeQuantity = detail.Quantity,
                                NewStock = item.CurrentStock,
                                Timestamp = DateTime.Now,
                            };
                            _context.InventoryLogs.Add(log);
                        }
                    }

                    _context.SaveChanges(); // Lưu tất cả các thay đổi vào cơ sở dữ liệu.
                    transaction.Commit();   // Hoàn tất transaction.
                    
                    TempData["Success"] = "Nhập kho thành công.";
                    return RedirectToAction("Index", "Home");
                }
                catch (Exception ex)
                {
                    // Có lỗi xảy ra. Hoàn tác lại transaction.
                    transaction.Rollback();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    // Chuẩn bị lại dữ liệu cho form.
                    ViewBag.Suppliers = new SelectList(_context.Suppliers, "Id", "Name");
                    return View(model);
                }
            }
        }

        // PHẦN XỬ LÝ XUẤT KHO

        [HttpGet]
        public IActionResult CreateOutbound()
        {
            // Chuẩn bị dữ liệu vật tư và vị trí cho việc xuất kho.
            ViewBag.Items = _context.Items.ToList();
            ViewBag.Locations = _context.Locations.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateOutbound(WarehouseTransactionViewModel model)
        {
            // Bắt đầu một transaction để đảm bảo tính toàn vẹn dữ liệu.
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // 1. Tạo phiếu xuất kho chính.
                    var receipt = new OutboundReceipt
                    {
                        CreatedDate = model.Date,
                        Notes = model.Remarks,
                        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    };
                    _context.OutboundReceipts.Add(receipt);
                    _context.SaveChanges();

                    // 2. Duyệt qua từng chi tiết.
                    foreach (var detail in model.Details)
                    {
                        var item = _context.Items.Find(detail.ItemId);
                        
                        // b. Kiểm tra số lượng tồn kho.
                        if (item == null || item.CurrentStock < detail.Quantity)
                        {
                            // Không đủ hàng tồn kho để thực hiện yêu cầu.
                            transaction.Rollback(); // Hoàn tác lại tất cả các thay đổi trong transaction hiện tại.
                            ModelState.AddModelError("", $"Sản phẩm {item?.Name ?? "N/A"} không đủ hàng tồn (Tồn kho: {item?.CurrentStock}, Yêu cầu: {detail.Quantity}).");
                            
                            // Tải lại giao diện và hiển thị thông báo lỗi.
                            ViewBag.Items = _context.Items.ToList();
                            ViewBag.Locations = _context.Locations.ToList();
                            return View(model);
                        }

                        // c. Giảm số lượng tồn kho.
                        item.CurrentStock -= detail.Quantity;
                        _context.Items.Update(item);

                        // d. Lưu chi tiết phiếu xuất kho.
                        var receiptDetail = new OutboundReceiptDetail
                        {
                            OutboundReceiptId = receipt.Id,
                            ItemId = detail.ItemId,
                            Quantity = detail.Quantity,
                            LocationId = detail.LocationId
                        };
                        _context.OutboundReceiptDetails.Add(receiptDetail);

                        // e. Ghi lại lịch sử xuất kho.
                        var log = new InventoryLog
                        {
                            ItemId = detail.ItemId,
                            ActionType = "OUTBOUND",
                            ChangeQuantity = -detail.Quantity, // Sử dụng giá trị âm để thể hiện giao dịch xuất kho.
                            NewStock = item.CurrentStock,
                            Timestamp = DateTime.Now,
                        };
                        _context.InventoryLogs.Add(log);
                    }

                    _context.SaveChanges();
                    transaction.Commit(); // Hoàn tất transaction.

                    TempData["Success"] = "Xuất kho thành công.";
                    return RedirectToAction("Index", "Home");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Lỗi nghiêm trọng: " + ex.Message);
                    ViewBag.Items = _context.Items.ToList();
                    ViewBag.Locations = _context.Locations.ToList();
                    return View(model);
                }
            }
        }
    }
}