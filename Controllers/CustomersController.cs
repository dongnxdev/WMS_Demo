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
    public class CustomersController : Controller
    {
        private readonly WmsDbContext _context;
        private const int DefaultPageSize = 10;

        public CustomersController(WmsDbContext context)
        {
            _context = context;
        }

        // GET: Lấy danh sách khách hàng.
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            // Lưu lại bộ lọc tìm kiếm để hiển thị trên view.
            ViewData["CurrentFilter"] = searchString;

            var customers = _context.Customers.AsNoTracking(); // Tối ưu hiệu năng: không theo dõi thay đổi của đối tượng.

            if (!string.IsNullOrEmpty(searchString))
            {

                var searchLower = searchString.ToLower();
                customers = customers.Where(s => s.Name.ToLower().Contains(searchLower) ||
                                         s.PhoneNumber.ToLower().Contains(searchLower));
            }

            customers = customers.OrderByDescending(i => i.Id);

            return View(await PaginatedList<Customer>.CreateAsync(customers, pageNumber ?? 1, DefaultPageSize));

        }

        // GET: Lấy chi tiết thông tin một khách hàng.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Hiển thị form để tạo mới khách hàng.
        public IActionResult Create()
        {
            return View();
        }

        // POST: Xử lý việc tạo mới một khách hàng.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,PhoneNumber")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    customer.Name = customer.Name?.Trim();
                    _context.Add(customer);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Thêm mới thành công: {customer.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    TempData["Error"] = $"Thêm mới thất bại: {customer.Name}";
                }
            }
            return View(customer);
        }

        // GET: Hiển thị form để chỉnh sửa khách hàng.
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        // POST: Xử lý việc cập nhật một khách hàng.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,PhoneNumber")] Customer customer)
        {
            if (id != customer.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCustomer = await _context.Customers.FindAsync(id);
                    if (existingCustomer == null) return NotFound();
                    // Cập nhật từng thuộc tính của đối tượng.
                    existingCustomer.Name = customer.Name;
                    existingCustomer.Address = customer.Address;
                    existingCustomer.PhoneNumber = customer.PhoneNumber;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật thành công: {existingCustomer.Name}";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.Id)) return NotFound();
                    else throw;
                }
                catch (Exception)
                {
                    TempData["Error"] = $"Cập nhật thất bại: {customer.Name}";
                }
            }
            return View(customer);
        }

        // GET: Hiển thị trang xác nhận xóa khách hàng.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null) return NotFound();

            return View(customer);
        }

        // POST: Xử lý xóa khách hàng.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                // Trường hợp khách hàng đã được xóa bởi một tiến trình khác.
                TempData["Error"] = "Không tìm thấy khách hàng để xóa. Có thể đã bị xóa trước đó.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                bool hasRelativeData = await _context.OutboundReceipts.AnyAsync(or => or.CustomerId == id);
                if (hasRelativeData)
                {
                    TempData["Error"] = $"Không thể xóa khách hàng {customer.Name} vì có dữ liệu liên quan.";
                    return RedirectToAction(nameof(Index));
                }
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Xóa thành công: {customer.Name}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xóa thất bại: {customer.Name}. Lỗi hệ thống: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));

        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
