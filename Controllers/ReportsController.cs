using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS_Demo.Data;
using WMS_Demo.Models;
using WMS_Demo.Helpers;
using WMS_Demo.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

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

        // GET: InventoryLog (Danh sách lịch sử có phân trang & tìm kiếm)
        public async Task<IActionResult> Index(
    string sortOrder,
    string currentFilter,
    string searchString,
    string actionType, // <--- Thêm tham số này
    int? pageNumber)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["DateSortParm"] = String.IsNullOrEmpty(sortOrder) ? "date_desc" : "";
            ViewData["ItemSortParm"] = sortOrder == "Item" ? "item_desc" : "Item";

            // Lưu lại trạng thái của ActionType để hiển thị lại trên Select2
            ViewData["CurrentActionType"] = actionType;

            if (searchString != null)
            {
                pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;

            var logs = _context.InventoryLogs.Include(l => l.Item).AsQueryable();

            // 1. Ưu tiên lọc theo ActionType trước
            if (!string.IsNullOrEmpty(actionType))
            {
                if (actionType == "INBOUND")
                {
                    var inboundTypes = new[] { "INBOUND", "INBOUND_REVERT" };
                    logs = logs.Where(l => inboundTypes.Contains(l.ActionType));
                }
                else if (actionType == "OUTBOUND")
                {
                    var outboundTypes = new[] { "OUTBOUND", "OUTBOUND_REVERT" };
                    logs = logs.Where(l => outboundTypes.Contains(l.ActionType));
                }
                }

            // 2. Sau đó mới tìm kiếm theo từ khóa
            if (!String.IsNullOrEmpty(searchString))
            {
                logs = logs.Where(s => s.Item.Name.Contains(searchString)
                                       || s.Item.Code.Contains(searchString)
                                       || s.ActionType.Contains(searchString));
            }

            switch (sortOrder)
            {
                case "Item":
                    logs = logs.OrderBy(s => s.Item.Name);
                    break;
                case "item_desc":
                    logs = logs.OrderByDescending(s => s.Item.Name);
                    break;
                case "date_desc":
                    logs = logs.OrderByDescending(s => s.Timestamp);
                    break;
                default:
                    logs = logs.OrderByDescending(s => s.Timestamp); // Mặc định cái mới nhất lên đầu
                    break;
            }

            int pageSize = 20;
            return View(await PaginatedList<InventoryLog>.CreateAsync(logs.AsNoTracking(), pageNumber ?? 1, pageSize));
        }
        // GET: InventoryLog/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var log = await _context.InventoryLogs
                .Include(l => l.Item)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            return View(log);
        }

        // GET: InventoryLog/Report (Báo cáo chuyên nghiệp)
        public async Task<IActionResult> Report(DateTime? fromDate, DateTime? toDate)
        {
            // Mặc định xem tháng hiện tại nếu không chọn
            var start = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = toDate ?? DateTime.Now;

            // 1. Tính tổng giá trị tồn kho hiện tại (Snapshot - không phụ thuộc ngày tháng lọc)
            // Giá trị kho = Sum(CurrentStock * CurrentCost) từ bảng Item
            var totalInventoryValue = await _context.Items
                .SumAsync(i => i.CurrentStock * i.CurrentCost);

            // 2. Lấy dữ liệu Log trong khoảng thời gian để tính Doanh thu & Lợi nhuận
            // Chỉ quan tâm đến OUTBOUND (Xuất kho bán hàng/sản xuất)
            var salesLogs = await _context.InventoryLogs
                .Include(l => l.Item)
                .Where(l => l.Timestamp >= start && l.Timestamp <= end
                            && l.ActionType == "OUTBOUND")
                .ToListAsync();

            // Tính toán KPI
            // Lưu ý: ChangeQuantity trong Outbound thường là số âm, nên ta lấy Abs để tính doanh thu
            decimal totalRevenue = salesLogs.Sum(x => Math.Abs(x.ChangeQuantity) * x.TransactionPrice);

            // Giá vốn (COGS) = Số lượng xuất * Giá vốn bình quân tại thời điểm xuất (MovingAverageCost)
            decimal totalCOGS = salesLogs.Sum(x => Math.Abs(x.ChangeQuantity) * x.MovingAverageCost);

            // 3. Phân tích Top sản phẩm hiệu quả
            var itemPerformance = salesLogs
                .GroupBy(x => new { x.ItemId, x.Item.Name, x.Item.Code })
                .Select(g => new ItemPerformanceMetrics
                {
                    ItemName = g.Key.Name,
                    ItemCode = g.Key.Code,
                    SoldQuantity = g.Sum(x => Math.Abs(x.ChangeQuantity)),
                    Revenue = g.Sum(x => Math.Abs(x.ChangeQuantity) * x.TransactionPrice),
                    Profit = g.Sum(x => (Math.Abs(x.ChangeQuantity) * x.TransactionPrice) - (Math.Abs(x.ChangeQuantity) * x.MovingAverageCost))
                })
                .OrderByDescending(x => x.Profit) // Sắp xếp theo lợi nhuận cao nhất
                .Take(10) // Lấy top 10
                .ToList();

            var viewModel = new InventoryReportViewModel
            {
                FromDate = start,
                ToDate = end,
                TotalInventoryValue = totalInventoryValue,
                TotalRevenue = totalRevenue,
                TotalCostOfGoodsSold = totalCOGS,
                TopSellingItems = itemPerformance
            };

            return View(viewModel);
        }
    }
}