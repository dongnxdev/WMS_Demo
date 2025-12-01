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
    public class SuppliersController : Controller
    {
        private readonly WmsDbContext _context;
        private const int DefaultPageSize = 10;

        public SuppliersController(WmsDbContext context)
        {
            _context = context;
        }

        // GET: Lấy danh sách tất cả các nhà cung cấp.
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;

            var suppliers = _context.Suppliers.AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchLower = searchString.ToLower();
                suppliers = suppliers.Where(s => s.Name.ToLower().Contains(searchLower) ||
                                         s.PhoneNumber.ToLower().Contains(searchLower));
            }

            suppliers = suppliers.OrderByDescending(i => i.Id);

            return View(await PaginatedList<Supplier>.CreateAsync(suppliers, pageNumber ?? 1, DefaultPageSize));
        }

        // GET: Lấy thông tin chi tiết của một nhà cung cấp cụ thể.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // GET: Hiển thị form để tạo mới một nhà cung cấp.
        public IActionResult Create()
        {
            return View();
        }

        // POST: Xử lý việc tạo mới một nhà cung cấp.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,PhoneNumber")] Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    supplier.Name = supplier.Name?.Trim();
                    _context.Add(supplier);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Thêm mới thành công: {supplier.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Thêm mới thất bại: {supplier.Name}";
                }
            }
            return View(supplier);
        }

        // GET: Hiển thị form để chỉnh sửa một nhà cung cấp đã có.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        // POST: Xử lý việc cập nhật một nhà cung cấp đã có.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,PhoneNumber")] Supplier supplier)
        {
            if (id != supplier.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSupplier = await _context.Suppliers.FindAsync(id);
                    if (existingSupplier == null) return NotFound();

                    existingSupplier.Name = supplier.Name;
                    existingSupplier.Address = supplier.Address;
                    existingSupplier.PhoneNumber = supplier.PhoneNumber;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật thành công: {existingSupplier.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.Id)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Cập nhật thất bại: {supplier.Name}";
                }
            }
            return View(supplier);
        }

        // GET: Hiển thị trang xác nhận xóa một nhà cung cấp.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var supplier = await _context.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (supplier == null) return NotFound();

            return View(supplier);
        }

        // POST: Xử lý việc xóa một nhà cung cấp.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                TempData["Error"] = "Không tìm thấy nhà cung cấp để xóa. Có thể đã bị xóa trước đó.";
                return RedirectToAction(nameof(Index));
            }

            bool hasRelatedData = await _context.InboundReceipts.AnyAsync(ir => ir.SupplierId == id);
            if (hasRelatedData)
            {
                TempData["Error"] = $"Không thể xóa nhà cung cấp '{supplier.Name}' vì đã có lịch sử nhập hàng.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Xóa thành công: {supplier.Name}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xóa thất bại: {supplier.Name}. Lỗi hệ thống: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.Id == id);
        }
    }
}
