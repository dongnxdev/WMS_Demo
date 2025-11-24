using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WMS_Demo.ViewModels
{
    /// <summary>
    /// ViewModel dùng chung cho các giao dịch nhập và xuất kho.
    /// </summary>
    public class WarehouseTransactionViewModel
    {
        // Thông tin chung của phiếu.
        [Required(ErrorMessage = "Vui lòng nhập ngày.")]
        public DateTime Date { get; set; } = DateTime.Now;

        public string? Remarks { get; set; } // Ghi chú cho phiếu.

        [Display(Name = "Đối tác")] // ID của nhà cung cấp hoặc khách hàng.
        public int? BusinessPartnerId { get; set; }

        // Danh sách các chi tiết hàng hóa.
        public List<TransactionDetailViewModel> Details { get; set; } = new List<TransactionDetailViewModel>();
    }

    /// <summary>
    /// ViewModel cho chi tiết của một giao dịch.
    /// </summary>
    public class TransactionDetailViewModel
    {
        public int ItemId { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải là số dương.")]
        public int Quantity { get; set; }
        
        public int LocationId { get; set; }
    }
}