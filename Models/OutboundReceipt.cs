using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    /// <summary>
    /// Đại diện cho một phiếu xuất kho.
    /// </summary>
    public class OutboundReceipt
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Ngày tạo phiếu.

        public int CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; } // Tham chiếu đến khách hàng.

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey(nameof(UserId))]
        public ApplicationUser? CreatedBy { get; set; } // Người tạo phiếu.

        public string? Notes { get; set; }

        // Danh sách các chi tiết của phiếu xuất.
        public ICollection<OutboundReceiptDetail> Details { get; set; } = new List<OutboundReceiptDetail>();
    }
}
