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

        // GET: Lịch sử kho (hỗ trợ lọc, sắp xếp, phân trang).
        public async Task<IActionResult> Index(
            string sortOrder,
            string currentFilter,
            string searchString,
            string actionType,
            int? pageNumber)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["DateSortParm"] = String.IsNullOrEmpty(sortOrder) ? "date_desc" : "";
            ViewData["ItemSortParm"] = sortOrder == "Item" ? "item_desc" : "Item";

            // Giữ lại bộ lọc loại hành động để hiển thị trên view.
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

            // 1. Lọc theo loại hành động.
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

            // 2. Lọc theo từ khóa tìm kiếm.
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
                    logs = logs.OrderByDescending(s => s.Timestamp); // Mặc định sắp xếp theo thời gian mới nhất.
                    break;
            }

            int pageSize = 20;
            return View(await PaginatedList<InventoryLog>.CreateAsync(logs.AsNoTracking(), pageNumber ?? 1, pageSize));
        }
        // GET: Chi tiết một log lịch sử kho.
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

        // GET: Báo cáo tổng hợp tồn kho.
        public async Task<IActionResult> Report(DateTime? fromDate, DateTime? toDate)
        {
            // Mặc định khoảng thời gian là tháng hiện tại.
            var start = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = toDate ?? DateTime.Now;

            // 1. Tính tổng giá trị tồn kho hiện tại (Snapshot).
            // Giá trị kho = Sum(Tồn kho * Giá vốn) của tất cả sản phẩm.
            var totalInventoryValue = await _context.Items
                .SumAsync(i => i.CurrentStock * i.CurrentCost);

            // 2. Lấy log xuất kho trong khoảng thời gian để tính toán.
            // Chỉ xét các giao dịch 'OUTBOUND'.
            var salesLogs = await _context.InventoryLogs
                .Include(l => l.Item)
                .Where(l => l.Timestamp >= start && l.Timestamp <= end
                            && l.ActionType == "OUTBOUND")
                .ToListAsync();

            // 3. Tính toán các chỉ số KPI.
            // Lưu ý: ChangeQuantity của OUTBOUND là số âm.
            decimal totalRevenue = salesLogs.Sum(x => Math.Abs(x.ChangeQuantity) * x.TransactionPrice);

            // Giá vốn (COGS) được tính dựa trên giá vốn bình quân tại thời điểm xuất.
            decimal totalCOGS = salesLogs.Sum(x => Math.Abs(x.ChangeQuantity) * x.MovingAverageCost);

            // 4. Phân tích Top 10 sản phẩm lợi nhuận cao nhất.
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
                .OrderByDescending(x => x.Profit) // Sắp xếp theo lợi nhuận giảm dần.
                .Take(10) // Lấy 10 sản phẩm đầu tiên.
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