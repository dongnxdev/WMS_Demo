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

        // GET: Retrieves a list of all customers.
        public async Task<IActionResult> Index(string searchString, int? pageNumber)
        {
            // Giữ lại giá trị search để hiển thị lại trên View
            ViewData["CurrentFilter"] = searchString;

            var customers = _context.Customers.AsNoTracking(); // Tối ưu hóa: Chỉ đọc, không theo dõi thay đổi

            if (!string.IsNullOrEmpty(searchString))
            {

                var searchLower = searchString.ToLower();
                customers = customers.Where(s => s.Name.ToLower().Contains(searchLower) ||
                                         s.PhoneNumber.ToLower().Contains(searchLower));
            }

            customers = customers.OrderByDescending(i => i.Id);

            return View(await PaginatedList<Customer>.CreateAsync(customers, pageNumber ?? 1, DefaultPageSize));

        }

        // GET: Retrieves the details of a specific customer.
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

        // GET: Displays the form to create a new customer.
        public IActionResult Create()
        {
            return View();
        }

        // POST: Handles the creation of a new customer.
        // Binds the specified properties from the form to the Customer model.
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
                catch (Exception ex)
                {
                    TempData["Error"] = $"Thêm mới thất bại: {customer.Name}";
                    // ModelState.AddModelError("", $"Lỗi hệ thống: {ex.Message}");
                }
            }
            return View(customer);
        }

        // GET: Displays the form to edit an existing customer.
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

        // POST: Handles the update of an existing customer.
        // Binds the specified properties from the form to the Customer model.
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
                    // Update từng trường 
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
                catch (Exception ex)
                {
                    TempData["Error"] = $"Cập nhật thất bại: {customer.Name}";
                    // ModelState.AddModelError("", $"Lỗi cập nhật: {ex.Message}");
                }
            }
            return View(customer);
        }

        // GET: Displays the confirmation page for deleting a customer.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null) return NotFound();

            return View(customer);
        }

        // POST: Handles the deletion of a customer.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                // customer đã bị xóa bởi một request khác
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
