using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Data;

namespace WMS_Demo.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly WmsDbContext _context;

        public ReportsController(WmsDbContext context)
        {
            _context = context;
        }

        // Báo cáo tồn kho hiện tại
        public IActionResult CurrentInventory()
        {
            // Lấy danh sách, sort theo tên cho dễ nhìn
            // Include thêm Category hoặc Supplier nếu thích (ở đây em demo đơn giản)
            var model = _context.Items
                                .OrderBy(i => i.Name)
                                .ToList();
            return View(model);
        }

        // Lịch sử giao dịch của 1 sản phẩm (Thẻ kho)
        public IActionResult ItemHistory(int itemId)
        {
            var item = _context.Items.Find(itemId);
            if (item == null) return NotFound("Không tìm thấy sản phẩm, chắc nó bốc hơi rồi.");

            // Lấy log, sắp xếp mới nhất lên đầu
            var history = _context.InventoryLogs
                                  .Where(l => l.ItemId == itemId)
                                  .OrderByDescending(l => l.Timestamp)
                                  .ToList();

            // Truyền dữ liệu sang View bằng ViewBag hoặc tạo ViewModel riêng cũng được
            // Ở đây em dùng ViewBag cho nhanh gọn lẹ (như cách người yêu cũ anh trở mặt)
            ViewBag.ItemName = item.Name;
            ViewBag.ItemCode = item.Code;
            ViewBag.CurrentStock = item.CurrentStock;

            return View(history);
        }
    }
}