using System;
using System.Collections.Generic;

namespace WMS_Demo.ViewModels
{
    public class InventoryReportViewModel
    {
        // Bộ lọc thời gian
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // KPI Tổng quan
        public decimal TotalInventoryValue { get; set; } // Tổng giá trị tồn kho hiện tại (Tài sản)
        public decimal TotalRevenue { get; set; }        // Tổng doanh thu (trong khoảng thời gian)
        public decimal TotalCostOfGoodsSold { get; set; } // Giá vốn hàng bán (COGS)
        public decimal GrossProfit => TotalRevenue - TotalCostOfGoodsSold; // Lợi nhuận gộp
        public decimal ProfitMargin => TotalRevenue > 0 ? (GrossProfit / TotalRevenue) * 100 : 0; // Tỷ suất lợi nhuận

        // Chi tiết hiệu quả theo từng sản phẩm (Top bán chạy/Lợi nhuận cao)
        public List<ItemPerformanceMetrics> TopSellingItems { get; set; } = new List<ItemPerformanceMetrics>();
    }

    public class ItemPerformanceMetrics
    {
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public decimal SoldQuantity { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
    }
}