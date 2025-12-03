using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Data;
using WMS_Demo.Models;
using WMS_Demo.Helpers;
using Microsoft.AspNetCore.Authorization;
namespace WMS_Demo.Controllers
{
     [Authorize]
    public class ItemsController : Controller
    {
        private readonly WmsDbContext _context;
        private const int DefaultPageSize = 10;

        public ItemsController(WmsDbContext context)
        {
            _context = context;
        }

        // GET: Lấy danh sách vật tư.
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            // Lưu lại bộ lọc tìm kiếm để hiển thị trên view.
            ViewData["CurrentFilter"] = searchString;

            var items = _context.Items.AsNoTracking(); // Tối ưu hiệu năng: không theo dõi thay đổi của đối tượng.

            if (!string.IsNullOrEmpty(searchString))
            {

                var searchLower = searchString.ToLower();
                items = items.Where(s => s.Name.ToLower().Contains(searchLower) ||
                                         s.Code.ToLower().Contains(searchLower));
            }

            items = items.OrderByDescending(i => i.Id);

            return View(await PaginatedList<Item>.CreateAsync(items, pageNumber ?? 1, DefaultPageSize));
        }

        // GET: Lấy chi tiết thông tin vật tư.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();

            return View(item);
        }

        // GET: Hiển thị form tạo mới vật tư.
        public IActionResult Create()
        {
            return View();
        }

        // POST: Xử lý tạo mới vật tư.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Code,Unit,SafetyStock,CurrentStock")] Item item)
        {
            // Kiểm tra trùng mã vật tư trước khi xác thực model.
            var codeSKU=item.Code?.Trim();
            if (await _context.Items.AnyAsync(i => i.Code == item.Code))
            {
                // Thêm lỗi vào ModelState nếu mã đã tồn tại.
                ModelState.AddModelError("Code", "Mã vật tư này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    item.CurrentStock = 0; // Tồn kho ban đầu của vật tư mới luôn là 0.
                    _context.Add(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Thêm mới thành công: {item.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    TempData["Error"] = $"Thêm mới thất bại: {item.Name}";
                }
            }
            // Nếu có lỗi, trả về view với dữ liệu đã nhập.
            return View(item);
        }

        // GET: Hiển thị form chỉnh sửa vật tư.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Items.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        // POST: Xử lý cập nhật vật tư.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Unit,Code,SafetyStock")] Item item)
        {

            if (id != item.Id) return NotFound();

            // Ghi chú: Mã vật tư (SKU) không được phép thay đổi sau khi tạo.
            if (ModelState.IsValid)
            {

                try
                {
                    // Lấy đối tượng gốc từ database để cập nhật.
                    var existingItem = await _context.Items.FindAsync(id);
                    if (existingItem == null) return NotFound();

                    // Cập nhật các thuộc tính được phép thay đổi.
                    existingItem.Name = item.Name;
                    existingItem.Unit = item.Unit;
                    existingItem.SafetyStock = item.SafetyStock;
                    // Tồn kho (CurrentStock) không được cập nhật trực tiếp tại đây.

                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật thành công: {item.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ItemExists(item.Id)) return NotFound();
                    else throw;
                }
                catch (Exception)
                {
                    TempData["Error"] = $"Cập nhật thất bại: {item.Name}";
                }
            }
            return View(item);
        }

        // GET: Hiển thị trang xác nhận xóa vật tư.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (item == null) return NotFound();

            return View(item);
        }

        // POST: Xử lý xóa vật tư.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                // Trường hợp vật tư đã được xóa bởi một tiến trình khác.
                TempData["Error"] = "Không tìm thấy vật tư để xóa. Có thể đã bị xóa trước đó.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra ràng buộc dữ liệu trước khi xóa.
            bool hasRelatedData = await _context.InboundReceiptDetails.AnyAsync(d => d.ItemId == id)
                       || await _context.OutboundReceiptDetails.AnyAsync(d => d.ItemId == id)
                       || await _context.InventoryLogs.AnyAsync(l => l.ItemId == id);

            if (hasRelatedData)
            {
                TempData["Error"] = $"Không thể xóa '{item.Name}' vì đã có lịch sử giao dịch/tồn kho.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa: {item.Name}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi xóa: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ItemExists(int id)
        {
            return _context.Items.Any(e => e.Id == id);
        }
    }
}