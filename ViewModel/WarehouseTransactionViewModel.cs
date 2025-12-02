using System.ComponentModel.DataAnnotations;
using WMS_Demo.Models;
using WMS_Demo.Helpers; // Để dùng PaginatedList nếu cần trong view, hoặc dùng IPagedList

namespace WMS_Demo.ViewModels
{
    // 1. ViewModel cho màn hình Index (Danh sách phiếu)
    public class ReceiptIndexViewModel
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ReferenceCode { get; set; } // Mã phiếu (ví dụ: PN-001)
        public string PartnerName { get; set; } // Tên NCC hoặc Khách hàng
        public string CreatedBy { get; set; }
        public string Notes { get; set; }
    }

    // 2. ViewModel cho màn hình Create/Edit (Dùng chung cấu trúc Master-Detail)
    public class ReceiptCreateViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Ngày tạo không được để trống")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Now;

        [Display(Name = "Đối tác (NCC/KH)")]
        [Required(ErrorMessage = "Vui lòng chọn đối tác")]
        public int PartnerId { get; set; }
        
        // Trường này để hiển thị lại tên khi Validation fail (Fix Re-Index issue)
        public string? PartnerName { get; set; } 

        public string? Notes { get; set; }

        // Danh sách chi tiết
        public List<ReceiptDetailViewModel> Details { get; set; } = new List<ReceiptDetailViewModel>();
    }

    public class ReceiptDetailViewModel
    {
        public int ItemId { get; set; }
        public string? ItemName { get; set; } // Để hiển thị lại khi lỗi
        public string? ItemCode { get; set; }
        public string? Unit { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "Số lượng chỉ được có tối đa 2 chữ số thập phân.")]
        public decimal Quantity { get; set; }

        public int LocationId { get; set; }
        public string? LocationCode { get; set; } // Để hiển thị lại

        public decimal Price { get; set; } // Giá nhập hoặc giá xuất
    }
}