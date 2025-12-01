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

namespace WMS_Demo.Controllers
{
    public class ItemsController : Controller
    {
        private readonly WmsDbContext _context;
        private const int DefaultPageSize = 10;

        public ItemsController(WmsDbContext context)
        {
            _context = context;
        }

        // GET: Items
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            // Giữ lại giá trị search để hiển thị lại trên View
            ViewData["CurrentFilter"] = searchString;

            var items = _context.Items.AsNoTracking(); // Tối ưu hóa: Chỉ đọc, không theo dõi thay đổi

            if (!string.IsNullOrEmpty(searchString))
            {

                var searchLower = searchString.ToLower();
                items = items.Where(s => s.Name.ToLower().Contains(searchLower) ||
                                         s.Code.ToLower().Contains(searchLower));
            }

            items = items.OrderByDescending(i => i.Id);

            return View(await PaginatedList<Item>.CreateAsync(items, pageNumber ?? 1, DefaultPageSize));
        }

        // GET: Items/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();

            return View(item);
        }

        // GET: Items/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Items/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Code,Unit,SafetyStock,CurrentStock")] Item item)
        {
            // 1. Check trùng Code thủ công trước khi check ModelState
            var codeSKU=item.Code?.Trim();
            if (await _context.Items.AnyAsync(i => i.Code == item.Code))
            {
                // Thêm lỗi vào ModelState 
                ModelState.AddModelError("Code", "Mã vật tư này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    item.CurrentStock = 0; // Mặc định tồn kho ban đầu là 0 khi tạo mới
                    _context.Add(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Thêm mới thành công: {item.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Thêm mới thất bại: {item.Name}";
                    // ModelState.AddModelError("", $"Lỗi hệ thống: {ex.Message}");
                }
            }
            // Nếu lỗi, trả về View cùng dữ liệu đã nhập để user không phải nhập lại
            return View(item);
        }

        // GET: Items/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Items.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        // POST: Items/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Unit,Code,SafetyStock")] Item item)
        {

            if (id != item.Id) return NotFound();

            // // Check trùng code khi sửa (ngoại trừ chính nó) // Sửa đổi không cho đổi code sku
            // var duplicateCode = await _context.Items.AnyAsync(i => i.Code == item.Code && i.Id != id);
            // if (duplicateCode)
            // {
            //     ModelState.AddModelError("Code", "Mã vật tư đã được sử dụng bởi sản phẩm khác.");
            // }
            if (ModelState.IsValid)
            {

                try
                {
                    // Lấy thằng cũ lên để update an toàn
                    var existingItem = await _context.Items.FindAsync(id);
                    if (existingItem == null) return NotFound();

                    // Update từng trường 
                    existingItem.Name = item.Name;
                    // existingItem.Code = item.Code;
                    existingItem.Unit = item.Unit;
                    existingItem.SafetyStock = item.SafetyStock;
                    // Không update CurrentStock ở đây vì tồn kho phải qua nhập/xuất

                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật thành công: {item.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ItemExists(item.Id)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Cập nhật thất bại: {item.Name}";

                    // ModelState.AddModelError("", $"Lỗi cập nhật: {ex.Message}");
                }
            }
            return View(item);
        }

        // GET: Items/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (item == null) return NotFound();

            return View(item);
        }

        // POST: Items/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                // item đã bị xóa bởi một request khác
                TempData["Error"] = "Không tìm thấy vật tư để xóa. Có thể đã bị xóa trước đó.";
                return RedirectToAction(nameof(Index));
            }

            // Logic kiểm tra ràng buộc dữ liệu (Referential Integrity Check)

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