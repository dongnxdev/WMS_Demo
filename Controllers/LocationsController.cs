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
    public class LocationsController : Controller
    {
        private readonly WmsDbContext _context;
        private const int DefaultPageSize = 10;

        public LocationsController(WmsDbContext context)
        {
            _context = context;
        }

        // GET: Lấy danh sách tất cả các vị trí.
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;

            var locations = _context.Locations.AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchLower = searchString.ToLower();
                locations = locations.Where(s => s.Code.ToLower().Contains(searchLower) ||
                                         s.Description.ToLower().Contains(searchLower));
            }

            locations = locations.OrderByDescending(i => i.Id);

            return View(await PaginatedList<Location>.CreateAsync(locations, pageNumber ?? 1, DefaultPageSize));
        }

        // GET: Lấy thông tin chi tiết của một vị trí cụ thể.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        // GET: Hiển thị form để tạo mới một vị trí.
        public IActionResult Create()
        {
            return View();
        }

        // POST: Xử lý việc tạo mới một vị trí.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Code,Description")] Location location)
        {
            var code = location.Code?.Trim();
            if (await _context.Locations.AnyAsync(i => i.Code == code))
            {
                ModelState.AddModelError("Code", "Mã vị trí này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    location.Code = code;
                    _context.Add(location);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Thêm mới thành công: {location.Code}";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Thêm mới thất bại: {location.Code}";
                }
            }
            return View(location);
        }

        // GET: Hiển thị form để chỉnh sửa một vị trí đã có.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound();
            }
            return View(location);
        }

        // POST: Xử lý việc cập nhật một vị trí đã có.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,Description")] Location location)
        {
            if (id != location.Id) return NotFound();
            var code = location.Code?.Trim();
            if (await _context.Locations.AnyAsync(i => i.Code == code))
            {
                ModelState.AddModelError("Code", "Mã vị trí này đã tồn tại.");
            }
            if (ModelState.IsValid)
            {
                try
                {
                    var existingLocation = await _context.Locations.FindAsync(id);
                    if (existingLocation == null) return NotFound();

                    existingLocation.Code = code;
                    existingLocation.Description = location.Description;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật thành công: {existingLocation.Code}";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LocationExists(location.Id)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Cập nhật thất bại: {location.Code}";
                }
            }
            return View(location);
        }

        // GET: Hiển thị trang xác nhận xóa một vị trí.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var location = await _context.Locations.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (location == null) return NotFound();

            return View(location);
        }

        // POST: Xử lý việc xóa một vị trí.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                TempData["Error"] = "Không tìm thấy vị trí để xóa. Có thể đã bị xóa trước đó.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                bool hasRelativeData = await _context.OutboundReceiptDetails.AnyAsync(or => or.LocationId == id) || await _context.InboundReceiptDetails.AnyAsync(ir => ir.LocationId == id);
                if (hasRelativeData)
                {
                    TempData["Error"] = $"Không thể xóa location {location.Code} vì có dữ liệu liên quan.";
                    return RedirectToAction(nameof(Index));
                }
                _context.Locations.Remove(location);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Xóa thành công: {location.Code}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xóa thất bại: {location.Code}. Lỗi hệ thống: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool LocationExists(int id)
        {
            return _context.Locations.Any(e => e.Id == id);
        }
    }
}
